using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Driver;
using SIPServer.Models;

namespace SIPServer
{
    public class Logger
    {
        private readonly IMongoCollection<LogDocument> _logCollection;

        public Logger(string connectionString, string databaseName, string collectionName)
        {
            var client = new MongoClient(connectionString);
            var database = client.GetDatabase(databaseName);
            _logCollection = database.GetCollection<LogDocument>(collectionName);
        }
        public void Log(string callId, LogEntry logEntry)
        {
            Task.Run(() =>
            {
                var filter = Builders<LogDocument>.Filter.Eq(doc => doc.callId, callId);
                var update = Builders<LogDocument>.Update.Push(doc => doc.logs, logEntry);

                _logCollection.UpdateOne(filter, update);
            });
        }

        public void CreateLogDocument(LogDocument logDocument)
        {
            Task.Run(() =>
            { 
                
                _logCollection.InsertOne(logDocument);

            });
        }

    }
}
