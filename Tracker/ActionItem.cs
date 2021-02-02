namespace Tracker 
{
    /// <summary>
    /// Simple class to help with serialization and validation.
    /// Could easily be extended with generics in order to create additional flexibility
    /// </summary>
    public class ActionItem 
    {
        public string Action {get;set;}
        public decimal Time {get;set;}
    }
}