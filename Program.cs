using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using System.Threading;
using System.Net;

namespace Liquidity_pools_top_list
{
    class Pool  //класс объектов с данными о пулах ликвидности
    {
        public string Address { get; set; } //свойство с вдресом пула
        public decimal TVL { get; set; }    //свойство с TVL
        public Pool(string adress = null, decimal tvl = 0) {    //конструктор класса
            Address = adress;
            TVL = tvl;
        }
    };
    internal class Program
    {
        /* Метод для отображения топ-списка пулов ликвидности
         * tokenPairs - JSON-объект с информацией о пулах ликвидности
         * listSize - число элементов для вывода
         */
        static void ShowPoolsTopList(JObject tokenPairs, uint listSize = 25)
        {
            string tokenAddresses = ""; //строка со всеми адресами токенов
            string token0Address = "";  //строки с адресами 1-го и 2-го (0 и 1) токенов в пуле ликвидности
            string token1Address = "";
            foreach (var tokenPair in tokenPairs["data"]["pairs"])
            {
                token0Address = tokenPair["token0"]["id"]?.ToString();  //считываем адреса токенов из JSON-объекта
                token1Address = tokenPair["token1"]["id"]?.ToString();
                tokenAddresses += token0Address + "," + token1Address + ",";    //добавляем адреса токенов через запятую
            }
            tokenAddresses = tokenAddresses.Remove(tokenAddresses.Length - 1);  //удаляем лишнюю запятую в конце строки
            var httpClient = new HttpClient();
            //отправляем запрос к CoinGecko для получения стоимости токенов в USD:
            var response =  httpClient.GetAsync($"https://api.coingecko.com/api/v3/simple/token_price/ethereum?contract_addresses={tokenAddresses}&vs_currencies=usd").Result;
            if (response.IsSuccessStatusCode)
            {
                var pools = new List<Pool>();   //создаём список объектов класса Pool
                var result = response.Content.ReadAsStringAsync().Result;   //считываем ответ на запрос
                var resultJSON = JObject.Parse(result); //конвертируем ответ на запрос в JSON
                decimal token0Price;    //цена 1 токена в USD
                decimal token1Price;
                decimal token0TotalPrice;   //полные стоимости 1-го и 2-го токенов в пуле в USD
                decimal token1TotalPrice;
                decimal TVL;    //суммарная стоимость двух токенов в пуле в USD
                int count = 0;
                foreach (var tokenPair in tokenPairs["data"]["pairs"])
                {
                    if (count == listSize)    //прерываем цикл после добавления указанного числа пулов ликвидности
                        break;
                    token0Price = 0;
                    token1Price = 0;
                    token0Address = tokenPair["token0"]["id"]?.ToString();
                    token1Address = tokenPair["token1"]["id"]?.ToString();
                    //если в базе CoinGecko найдены цены в USD для обоих токенов, создаём объект Pool и добавляем его в список пулов:
                    if (resultJSON[token0Address]?["usd"] != null && resultJSON[token1Address]?["usd"] != null)
                    {
                        token0Price = resultJSON[token0Address]["usd"].Value<decimal>();
                        token1Price = resultJSON[token1Address]["usd"].Value<decimal>();
                        if (token0Price != 0 && token1Price != 0)
                        {
                            token0TotalPrice = token0Price * tokenPair["reserve0"].Value<decimal>();
                            token1TotalPrice = token1Price * tokenPair["reserve1"].Value<decimal>();
                            TVL = token0TotalPrice + token1TotalPrice;
                            var pool = new Pool(tokenPair["id"].ToString(), TVL);
                            pools.Add(pool);
                            count++;
                        }
                    }
                }
                pools.Sort((pool1, pool2) => pool2.TVL.CompareTo(pool1.TVL)); //сортировка пулов по убыванию TVL
                for (int i = 0; i < pools.Count; i++) //вывод списка пулов ликвидности
                {
                    Console.WriteLine($"{pools[i].Address}\t\t{(long)pools[i].TVL}");
                }
            }
            else
            {
                //в случае ошибки выводим её код
                Console.WriteLine("getTokenPriceInUSD Error: " + response.StatusCode);
            }
        }
        static void Main(string[] args)
        {
            //текст запроса к API Uniswap V2:
            var poolsQuery = "{\"query\": \"{ pairs(first: 75, orderBy: reserveUSD, orderDirection: desc) { id, token0 { id }, token1 { id }, reserve0, reserve1, reserveUSD } }\"}";
            var httpClient = new HttpClient();  //объект класса для отправки http-запросов
            //создаём запрос:
            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.thegraph.com/subgraphs/name/ianlapham/uniswap-v2-dev")
            {
                Content = new StringContent(poolsQuery, Encoding.UTF8, "application/json")
            };
            var response = httpClient.SendAsync(request).Result;    //отправка запроса
            if (response.IsSuccessStatusCode)
            {
                var result = response.Content.ReadAsStringAsync().Result;
                var resultJSON = JObject.Parse(result);
                if (resultJSON["errors"] == null)
                {
                    Console.WriteLine("Address\t\t\t\t\t\t\tTVL");
                    //вызов метода для отображения топ-списка пулов ликвидности:
                    ShowPoolsTopList(resultJSON, 25);
                    Console.ReadLine();
                }
                else
                    //если статус-код ОК, но запрос вернулся с ошибкой, выводим её:
                    Console.WriteLine($"Error: {resultJSON["errors"][0]["message"]}");
            }
            else
                //в случае ошибки выводим её код
                Console.WriteLine("Error:" + response.StatusCode);
        }
    }
}
