using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Tracker;
using Xunit;

namespace ActionTrackerTests
{
    public class ActionTrackerTests
    {
        [Fact]
        public async Task AddActionsAndGetResults_HappyPath()
        {
            // add several actions and test the average time
            var tracker = new ActionTracker();
            await tracker.AddActionAsync(await CreateItemAsync("jump", 100));
            await tracker.AddActionAsync(await CreateItemAsync("run", 75));
            await tracker.AddActionAsync(await CreateItemAsync("jump", 200));
            await tracker.AddActionAsync(await CreateItemAsync("run", 125));

            var actionLists = JsonSerializer.Deserialize<List<ActionItem>>(await tracker.GetStatsAsync());
            Assert.Equal(2, actionLists.Count);
            Assert.Equal(150, actionLists.Where(x => x.Action == "jump").First().Time);
            Assert.Equal(100, actionLists.Where(x => x.Action == "run").First().Time);
        }

        [Fact]
        public async Task AddActionWithInvalidJson() 
        {
            // test argument exception for invalid json
            var tracker = new ActionTracker();
            var exception = await Assert.ThrowsAsync<ArgumentException>(async () => await tracker.AddActionAsync("bad json"));
            Assert.Equal("Invalid json passed", exception.Message);
        }

        [Fact]
        public async Task AddActionWithInvalidTimeValue() 
        {
            // test argument exception for invalid time
            var tracker = new ActionTracker();
            await tracker.AddActionAsync(await CreateItemAsync("run", 0));

            var rangeException = await Assert.ThrowsAsync<ArgumentException>(async () => await tracker.AddActionAsync(await CreateItemAsync("run", -100)));
            Assert.Equal("Time must be 0 or greater", rangeException.Message);
        }

        [Fact]
        public async Task AddActionsWithConcurrency() 
        {
            // add 9 tasks that will run async because they AddActionAsync() methods are not awaited
            // wait on all of the task to complete and then test the results to make sure it handled the concurrent calls.
            var tracker = new ActionTracker();

            var tasks = new List<Task>();
            tasks.Add(tracker.AddActionAsync(await CreateItemAsync("jump",75)));
            tasks.Add(tracker.AddActionAsync(await CreateItemAsync("run", 75)));
            tasks.Add(tracker.AddActionAsync(await CreateItemAsync("skip", 75)));
            tasks.Add(tracker.AddActionAsync(await CreateItemAsync("jump", 100)));
            tasks.Add(tracker.AddActionAsync(await CreateItemAsync("run", 100)));
            tasks.Add(tracker.AddActionAsync(await CreateItemAsync("skip", 100)));
            tasks.Add(tracker.AddActionAsync(await CreateItemAsync("jump", 125)));
            tasks.Add(tracker.AddActionAsync(await CreateItemAsync("run", 125)));
            tasks.Add(tracker.AddActionAsync(await CreateItemAsync("skip", 125)));

            // make a call with an exception to make sure it doesn't call the others to fail.
            tasks.Add(tracker.AddActionAsync("not valid json"));

            // wait on all the tasks to complete in any given order
            var holderTask = Task.WhenAll(tasks.ToArray());
            try 
            {
                holderTask.Wait();
            }
            catch 
            {
                for (var i = 0; i < tasks.Count - 2; i++) 
                {
                    // make sure the first 9 tasks completed.
                    Assert.Equal(TaskStatus.RanToCompletion, tasks[i].Status);
                }

                // verify the last task faulted
                Assert.Equal(TaskStatus.Faulted, tasks[tasks.Count - 1].Status);
            }
            
            var items = JsonSerializer.Deserialize<List<ActionItem>>(await tracker.GetStatsAsync());
            Assert.Equal(3, items.Count);
            Assert.Equal(100, items.Where(x => x.Action == "jump").First().Time);
            Assert.Equal(100, items.Where(x => x.Action == "run").First().Time);
            Assert.Equal(100, items.Where(x => x.Action == "skip").First().Time);
        }

        private async Task<string> CreateItemAsync(string action, int time) {
            using (var stream = new MemoryStream()) {
                await JsonSerializer.SerializeAsync(stream, new ActionItem {Action = action, Time = time});
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }
    }
}
