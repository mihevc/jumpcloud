using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Tracker 
{
    public class ActionTracker
    {
        /// <summary>
        /// ConcurrentDictionary abstraction allows me to update the dictionary without having to do the locking myself.
        ///   Dictionary gives me the quickest AddAction() behavior but will create a little more latency on GetStats().
        ///   I could instead use some form of a list or an array in order to speed up GetStats if that is more important
        /// </summary>
        private ConcurrentDictionary<string, List<decimal>> _stats = new ConcurrentDictionary<string, List<decimal>>();
        
        /// <summary>
        /// Validate the json and validate the time value in the json. 
        ///   Throw an exception if they are not valid.
        /// 
        /// I store an array of times in the action item stored in the dictionary.  This allows me to get the most accurate
        ///   average time.  I could average the time as I add action but this would create more latency and I optimized for AddAction() versus the GetStats()
        /// 
        /// Also used async/await in order to increase asynchronous calls
        /// </summary>
        /// <param name="jsonItem">Json serialization of the ActionItem</param>
        public async Task AddActionAsync(string jsonItem) 
        {
            ActionItem item;

            // deserialize and validate the json parameter
            try 
            {
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonItem)))
                {
                    item = await JsonSerializer.DeserializeAsync<ActionItem>(stream);
                }
            }
            catch (JsonException) 
            {
                throw new ArgumentException("Invalid json passed");
            }

            if (item.Time < 0) 
            {
                throw new ArgumentException("Time must be 0 or greater");
            }
            
            // Dictionary access at best is O1 at worst is On.  
            //  It generally tends to be closer to the O1 versus the later.
            if (!_stats.ContainsKey(item.Action)) 
            {
                _stats.TryAdd(item.Action, new List<decimal>{item.Time});
            }
            else 
            {
                _stats[item.Action].Add(item.Time);
            }
        }

        /// <summary>
        /// I decided to do the summing here instead of during adding to optimize for adding.
        /// I do 
        /// </summary>
        /// <returns></returns>
        public async Task<string> GetStatsAsync() 
        {
            //  I convert dictionary to a list in order to get the desired serialized output.
            //  I could do this different if GetStats() needed to be optimized for speed.  
            //   I could use a Serilization formatter (maybe faster)
            //   I could have a background process working on pre-caching the results 
            //   I could calculate a cache during add asynchronously.  
            //     There's a chance a quick call to get after add could result in stale data especially as the dataset grows in size.
            var items = _stats.Select(x => new ActionItem 
                        {
                            Action = x.Key, 
                            Time = x.Value.Sum() / x.Value.Count
                        });
            
            // async serilization writes to a stream which I convert to a string.
            // serialization is always an expensive operation
            using (var stream = new MemoryStream()) 
            {
                await JsonSerializer.SerializeAsync(stream, items);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }
    }
}