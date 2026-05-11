using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Newtonsoft.Json;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace admin_web.Services.MondayTroubleShootServices
{
    public class RriskConfigUpdataService
    {
        private static readonly HttpClient client = new HttpClient();

        public static async Task<string> LoginAndPostData()
        {
            var (status, cookies) = await RcLogin();

            if (status == 200 && cookies.ContainsKey("satoken"))
            {
                var outMessage = await RcPostData(cookies["satoken"]);

                return outMessage;
            }
            else
            {
                return "更新失敗";
            }
        }

        private static async Task<(int, Dictionary<string, string>)> RcLogin()
        {
            var url = "https://riskrc.unicornfintech.com/api/auth/signIn";

            var parameters = new
            {
                username = "BensonLai",
                password = "e83ddddd5baed90648bb5abcfa2ea6e6"
            };

            var content = new StringContent(JsonConvert.SerializeObject(parameters), Encoding.UTF8, "application/json");

            var response = await client.PostAsync(url, content);
            var status = (int)response.StatusCode;

            var cookies = new Dictionary<string, string>();
            if (response.Headers.Contains("Set-Cookie"))
            {
                foreach (var cookie in response.Headers.GetValues("Set-Cookie"))
                {
                    var satoken = cookie.Split(';')[0].Split('=')[1];
                    cookies["satoken"] = satoken;
                }
            }

            return (status, cookies);
        }

        private static async Task<string> RcPostData(string satoken)
        {
            var url = "https://riskrc.unicornfintech.com/api/onceAppWall/reloadServer";

            var headers = new Dictionary<string, string>
            {
                { "Content-Type", "application/json" },
                { "Cookie", $"satoken={satoken}" }
            };

            var parameters = new
            {
                serverId = new[] { "MT5AU", "AU1", "VGPUK", "MT5PU3" },
                type = "selected"
            };

            var content = new StringContent(JsonConvert.SerializeObject(parameters), Encoding.UTF8, "application/json");

            var response = await client.PostAsync(url, content);

            await Task.Delay(180000);

            return await response.Content.ReadAsStringAsync();
        }
    }
}
