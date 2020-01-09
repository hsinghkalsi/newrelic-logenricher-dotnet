using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Configuration;
using System.Linq;

namespace NewRelic.LogEnrichers
{

    public class EntityModel
    {
        public int AccountId { get; set; }
        public string entityType { get; set; }
        public string Guid { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
    }

    public class AccountModel
    {
        public int Id { get; set; }
        public string LicenseKey { get; set; }
        public string Name { get; set; }
    }

    public interface INewRelicDataService
    {
        Task<EntityModel> GetEntityAsync(string serviceName, string licenseKey);
        Task<IEnumerable<AccountModel>> GetAccountsByIdAsync(params int[] accountIds);
    }


    public class NewRelicDataService : INewRelicDataService
    {

        private const string _entityQueryTemplate = "query($query: String!) { actor { entitySearch(query: $query) { count results { entities { accountId name entityType type guid }}}}}";
        private const string _accountQueryTemplate = "query($accountId: ID!) { actor {  account(id: $accountId) { id licenseKey name } } }";

        private readonly HttpClient _httpClient;
        private readonly NewRelicConfiguration _config;

        public NewRelicDataService(IConfiguration configProvider) : this(new NewRelicConfiguration(configProvider))
        {
        }

        public NewRelicDataService(NewRelicConfiguration config)
        {
            _config = config;

            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(20);         //TODO: Configure this

            //Ensures that DNS expires regularly.
            var sp = ServicePointManager.FindServicePoint(new Uri(_config.NerdGraphQueryUrl));
            sp.ConnectionLeaseTimeout = 60000;  // 1 minute
        }


        public async Task<IEnumerable<AccountModel>> GetAccountsByIdAsync(params int[] accountIds)
        {
            var result = new List<AccountModel>();

            var varsDic = new Dictionary<string, object>()
            {
            };

            var qry = new Dictionary<string, object>();
            qry["query"] = _accountQueryTemplate;
            qry["variables"] = varsDic;

            foreach (var accountId in accountIds.Distinct())
            {
                varsDic["accountId"] = accountId;
                var qryJson = JsonConvert.SerializeObject(qry);

                var account = await QueryForObject<AccountModel>(_config.NerdGraphQueryUrl, qryJson, "data", "actor", "account");

                if (account != null)
                {
                    result.Add(account);
                }
            }

            return result;
        }

        //TODO: put in feature requests to make this easier.
        public async Task<EntityModel> GetEntityAsync(string serviceName, string licenseKey)
        {
            //Query for the entities using this service name scoped to the caller's visibility
            var qry = new Dictionary<string, object>();
            qry["query"] = _entityQueryTemplate;
            qry["variables"] = new Dictionary<string, string>()
            {
                {"query",$"name LIKE '{serviceName}' AND domain in ('APM') AND type in ('APPLICATION')" }
            };

            var qryJson = JsonConvert.SerializeObject(qry);

            var entities = await QueryForCollection<EntityModel>(_config.NerdGraphQueryUrl, qryJson, "data", "actor", "entitySearch", "results", "entities");

            //Obtain the accounts from the entities
            var accountIds = entities.Select(x => x.AccountId).Distinct().ToArray();

            var accounts = await GetAccountsByIdAsync(accountIds);

            //Find out which account matches the config file's license key
            var matchAccount = accounts.FirstOrDefault(x => x.LicenseKey.Equals(licenseKey, StringComparison.OrdinalIgnoreCase));

            if (matchAccount == null)
            {
                return null;
            }

            //Return the first entity that matches that account.
            return entities.FirstOrDefault(x => x.AccountId == matchAccount.Id);
        }

        private async Task<TResponse> QueryForObject<TResponse>(string endpointUrl, string queryJson, params string[] propertyNames)
            where TResponse : class
        {
            var result = await Query(endpointUrl, queryJson);

            if (!result.IsSuccessStatusCode)
            {
                return null;
            }

            var resultJson = await result.Content.ReadAsStringAsync();

            var resultJsonObj = JObject.Parse(resultJson);

            var resultCollection = GetPropertyFromJObject<TResponse>(resultJsonObj, propertyNames);

            return resultCollection;
        }

        private async Task<TResponse[]> QueryForCollection<TRequest, TResponse>(string endpointUrl, TRequest queryDef, params string[] propertyNames)
             where TResponse : class
        {
            var queryJson = JsonConvert.SerializeObject(queryDef);

            return await QueryForCollection<TResponse>(endpointUrl, queryJson, propertyNames);
        }

        private async Task<TResponse[]> QueryForCollection<TResponse>(string endpointUrl, string queryJson, params string[] propertyNames)
            where TResponse : class
        {
            var result = await Query(endpointUrl, queryJson);

            if (!result.IsSuccessStatusCode)
            {
                return null;
            }

            var resultJson = await result.Content.ReadAsStringAsync();

            var resultJsonObj = JObject.Parse(resultJson);

            var resultCollection = GetPropertyFromJObjectAsCollection<TResponse>(resultJsonObj, propertyNames);

            return resultCollection;
        }

        private async Task<HttpResponseMessage> Query(string endpointUrl, string serializedPayload)
        {
            var serializedBytes = new UTF8Encoding().GetBytes(serializedPayload);

            using (var memoryStream = new MemoryStream(serializedBytes))
            {
                memoryStream.Position = 0;

                var streamContent = new StreamContent(memoryStream);
                streamContent.Headers.Add("Content-Type", "application/json");
                streamContent.Headers.ContentLength = memoryStream.Length;

                var requestMessage = new HttpRequestMessage(HttpMethod.Post, endpointUrl);
                requestMessage.Content = streamContent;
                requestMessage.Headers.Add("API-Key", _config.ApiKeyQuery);
                requestMessage.Method = HttpMethod.Post;

                var response = await _httpClient.SendAsync(requestMessage);

                return response;
            }
        }

        /// <summary>
        /// Navigates through a JSON object using a list of properties.
        /// At the final property expects an array which it will convert to T[]
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="jsonObj"></param>
        /// <param name="keys"></param>
        /// <returns></returns>
        private static T[] GetPropertyFromJObjectAsCollection<T>(JObject jsonObj, params string[] keys) where T : class
        {
            if (keys == null || keys.Length == 0 || !jsonObj.TryGetValue(keys[0], out var result))
            {
                return null;
            }

            if (keys.Length == 1)
            {
                if (!(result is JArray))
                {
                    return null;
                }

                var resultJArray = result as JArray;

                return resultJArray.Select(j => j.ToObject<T>()).ToArray();
            }


            if (!(result is JObject))
            {
                return null;
            }

            return GetPropertyFromJObjectAsCollection<T>(result as JObject, keys.Skip(1).ToArray());
        }

        /// <summary>
        /// Navigates through a JSON object using a list of properties.
        /// At the final property expects an object array which it will conttert to T
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="jsonObj"></param>
        /// <param name="keys"></param>
        /// <returns></returns>
        private static T GetPropertyFromJObject<T>(JObject jsonObj, params string[] keys) where T : class
        {
            if (keys == null || keys.Length == 0 || !jsonObj.TryGetValue(keys[0], out var result))
            {
                return null;
            }

            if (keys.Length == 1)
            {
                if (!(result is JObject))
                {
                    return null;
                }

                var resultJObject = result as JObject;

                return resultJObject.ToObject<T>();
            }

            if (!(result is JObject))
            {
                return null;
            }

            return GetPropertyFromJObject<T>(result as JObject, keys.Skip(1).ToArray());
        }
    }
}
