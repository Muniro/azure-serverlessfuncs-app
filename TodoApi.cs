using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos.Table;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ServerlessFuncs_app
{
    public static class TodoApi
    {
        //ok for dev env, in real time, scaling could cause issues and restart of the server!

        [FunctionName("CreateTodo")]
        public static async Task<IActionResult> CreateTodo(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "todo")] HttpRequest req,
            [Table("todos", Connection="AzureWebJobsStorage")] IAsyncCollector<TodoTableEntity> todoTable,
            ILogger log)
        {
            log.LogInformation("Creating a new todo item");
                      

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var input= JsonConvert.DeserializeObject<TodoCreateModel>(requestBody);

            var todo = new Todo() { TaskDescription = input.TaskDescription };
            await todoTable.AddAsync(todo.ToTableEntity());
           return new OkObjectResult(todo);
        }
        [FunctionName("GetTodos")]
        public static  async Task<IActionResult> GetTodos(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "todo")] HttpRequest req,
            [Table("todos", Connection = "AzureWebJobsStorage")] Microsoft.WindowsAzure.Storage.Table.CloudTable todoTable,
            ILogger log)
        {
            log.LogInformation("Getting list of todo item");
            var query = new Microsoft.WindowsAzure.Storage.Table.TableQuery<TodoTableEntity>();
            var segment = await todoTable.ExecuteQuerySegmentedAsync(query, null);
            return new OkObjectResult(segment.Select(Mappings.ToTodo));
        }


        [FunctionName("GetTodoById")]
        public static IActionResult GetTodoById(
           [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "todo/{id}")] HttpRequest req,
            [Table("todos", "TODO", "{id}", Connection = "AzureWebJobsStorage")] TodoTableEntity todo,
            ILogger log, string id)
        {
            log.LogInformation($"Getting todo item with Id {id}");
            if (todo == null)
            {
                return new NotFoundResult();
            }     
            return new OkObjectResult(todo.ToTodo());
        }

        [FunctionName("UpdateTodo")]
         public static async Task<IActionResult> UpdateTodo(
           [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "todo/{id}")] HttpRequest req,
            [Table("todos", Connection = "AzureWebJobsStorage")] Microsoft.WindowsAzure.Storage.Table.CloudTable todoTable,
            ILogger log, string id)
        {
            log.LogInformation($"Updating todo item with Id {id}");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var updated = JsonConvert.DeserializeObject<TodoUpdateModel>(requestBody);

            var findOperation = Microsoft.WindowsAzure.Storage.Table.TableOperation.Retrieve<TodoTableEntity>("TODO", id);
            var findResult = await todoTable.ExecuteAsync(findOperation);
            if (findResult == null)
            {
                return new NotFoundResult();
            }
            var existingRow = (TodoTableEntity)findResult.Result;
            existingRow.IsCompleted = updated.IsCompleted;
            if (!string.IsNullOrEmpty(updated.TaskDescription))
            {
                existingRow.TaskDescription = updated.TaskDescription;
            }
            var replaceOperation = Microsoft.WindowsAzure.Storage.Table.TableOperation.Replace(existingRow);
            await todoTable.ExecuteAsync(replaceOperation);

            return new OkObjectResult(existingRow.ToTodo());
        }


        [FunctionName("DeleteTodo")]
        public static async Task<IActionResult> DeleteTodo(
         [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "todo/{id}")] HttpRequest req,
         [Table("todos", Connection = "AzureWebJobsStorage")] Microsoft.WindowsAzure.Storage.Table.CloudTable todoTable,
         ILogger log, string id)
        {
            var deleteOperation = Microsoft.WindowsAzure.Storage.Table.TableOperation.Delete(
                new Microsoft.WindowsAzure.Storage.Table.TableEntity() { PartitionKey = "TODO", RowKey = id, ETag = "*" });
            try
            {
                var deletedResult = await todoTable.ExecuteAsync(deleteOperation);
            }
            catch(StorageException e) when (e.RequestInformation.HttpStatusCode == 404)
            {
                return new NotFoundResult();
            }
            return new OkResult();
        }
    }
}
