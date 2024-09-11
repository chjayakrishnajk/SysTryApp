using LiteDB;

namespace SysTryApp
{
    public class MouseData
    {
        [BsonId]
        public ObjectId Id { get; set; }
        public string LastMouse { get; set; }
    }
}