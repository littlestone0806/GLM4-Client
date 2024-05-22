using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GLM_BOT
{
    public class ZhipuAI
    {
        public async Task ChatCompletionsCreate(string apikey, string model, int max_tokens, string temperature, string top_p, List<Message> messages,Meta meta, bool stream, Func<string, Task> lineHandler, CancellationToken cancellationToken)
        {         
            var postdata = new
            {
                model = model,
                max_tokens = max_tokens,
                temperature = temperature,
                top_p = top_p,
                messages = messages,
                meta = meta,
                stream = stream
            };
            // 创建一个 HttpClient 实例
            using (var httpClient = new HttpClient())
            {
                var json = System.Text.Json.JsonSerializer.Serialize(postdata);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                var request = new HttpRequestMessage(HttpMethod.Post, "https://open.bigmodel.cn/api/paas/v4/chat/completions") { Content = content };
                // 添加认证头
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apikey);
                // 发送 POST 请求
                var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

                Stream str = await response.Content.ReadAsStreamAsync();
                var streamReader = new StreamReader(str, Encoding.UTF8);

                // 读取数据流
                while (!streamReader.EndOfStream)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {               
                        return;// 处理取消请求
                    }
                    try
                    {
                        string result = streamReader.ReadLine();//逐行读取
                                                                // 调用异步处理函数
                        await lineHandler(result);
                    }
                    catch { }
                }
            }          
        }

        public async Task ImagesGenerations(string apikey, string model, string prompt, string size, Action<string> lineHandler, CancellationToken cancellationToken)
        {
            var postdata = new
            {
                model = model,
                prompt = prompt,
                size = size
            };
            // 创建一个 HttpClient 实例
            var httpClient = new HttpClient();

            var json = System.Text.Json.JsonSerializer.Serialize(postdata);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var request = new HttpRequestMessage(HttpMethod.Post, "https://open.bigmodel.cn/api/paas/v4/images/generations") { Content = content };
            // 添加认证头
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apikey);
            // 发送 POST 请求
            var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

            Stream str = await response.Content.ReadAsStreamAsync();
            var streamReader = new StreamReader(str, Encoding.UTF8);

            // 读取数据流
            while (!streamReader.EndOfStream)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;// 处理取消请求
                }
                try
                {
                    string result = streamReader.ReadLine();//逐行读取
                                                            // 调用异步处理函数
                    lineHandler(result);
                }
                catch { }
            }
        }
    }
    
    public class Message
    {
        public string role { get; set; }
        public string content { get; set; }
    }
    public class Meta
    {
        public string user_info { get; set; }
        public string bot_info { get; set; }
        public string user_name { get; set; }
        public string bot_name { get; set; }
    }
}
