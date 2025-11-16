using Microsoft.Extensions.AI;
using OllamaSharp;
using Polly;
using Polly.Retry;
using HtmlAgilityPack;
using static System.Net.Mime.MediaTypeNames;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Tab;
using HtmlDocument = HtmlAgilityPack.HtmlDocument;
using System.Diagnostics;

namespace AIEnglishCoachWithAgent.Service
{
    public class NewsService
    {
        private readonly HttpClient _httpClient;

        private readonly AsyncRetryPolicy<HttpResponseMessage> _httpRetryPolicy;
        private readonly AsyncRetryPolicy<NewsContent> _llmRetryPolicy;
        private string _modelName;
        private string _ollamaUrl;

        public NewsService(string ollamaUrl = "http://localhost:11434", string modelName = "qwen2.5:3b")
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");

            _ollamaUrl = ollamaUrl;
            _modelName = modelName;

            // 定义 HTTP 重试策略
            _httpRetryPolicy = Policy
                // 处理 HttpRequestException (网络错误) 和 5XX 状态码
                .Handle<HttpRequestException>()
                .OrResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
                // 重试 3 次，使用指数退避
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (outcome, timespan, retryCount, context) =>
                    {
                        // ⚠️ 不要在这里读取 Content
                        Console.WriteLine($"重试第 {retryCount} 次");
        }
                );

            // 定义 LLM 重试
            _llmRetryPolicy = Policy<NewsContent>
                // 捕获所有潜在的瞬时错误：
                // 1. 网络错误（HttpClient内部）
                // 2. 客户端抛出的 API 错误（例如 429 状态码被客户端内部转换成异常）
                // 3. JSON 解析失败（如果 LLM 返回了不规范的字符串）
                .Handle<Exception>()
                .OrResult(r=>r==null)   // 如果 NewsContent 有可能返回 null 或默认值表示失败，可以添加 .OrResult(r => r == null)
                .WaitAndRetryAsync(3, retryAttempt =>
                    TimeSpan.FromSeconds(Math.Pow(1.5, retryAttempt))
                );
        }

        public async Task<NewsContent> GetNewsContentAsync(string url)
        {
            // 1. HTTP抓取
            HttpResponseMessage httpResponse = await _httpRetryPolicy.ExecuteAsync(async () =>
            {
                // 使用 SendAsync 获取响应消息，以便 Poliy 检查状态码
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                return await _httpClient.SendAsync(request);
            });

                // 检查最终响应是否成功 (Polly 已经重试过)
            httpResponse.EnsureSuccessStatusCode();

                // 手动读取内容
            var response = await httpResponse.Content.ReadAsStringAsync();

            // 2. 简单清理HTML 获取<body>
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(response);

            var bodyNode = htmlDoc.DocumentNode.SelectSingleNode("//body");

            var nodesToRemove = bodyNode.Descendants()
                                   .Where(n => n.Name == "script" || n.Name == "style")
                                   .ToList();

            foreach (var node in nodesToRemove)
            {
                node.Remove();
            }

            string bodyHtmlContent = "";
            if (bodyNode != null)
            {
                // 提取 <body> 节点内的所有 HTML 内容
                bodyHtmlContent = bodyNode.InnerHtml.Trim();
            }
            else
            {
                // 备用方案：如果找不到 <body>，使用整个已清理的文档内容（不理想但安全）                
                bodyHtmlContent = htmlDoc.DocumentNode.InnerHtml.Trim();
            }

            var cleanText = bodyHtmlContent.Substring(0, Math.Min(bodyHtmlContent.Length, 8000)); // 限制长度

            // 3. LLM提取
            // 假设 NewsContent 是一个包含 Title 和 Body 字段的结构
            var prompt = $@"**You are an expert, high-accuracy content extraction agent.** Your task is to analyze the provided raw HTML fragment from a news article's <body>. Scripts and styles have been removed, but all other structural HTML remains. **Your priority is accurate extraction and universal filtering.**

                    **Your Steps:**

                    1.  **Strict Filtering (High Priority):** Analyze the HTML structure, CSS classes, and IDs to locate the main article content. You must **STRICTLY ignore and remove all HTML and plain text content** associated with:
                        * **Navigation:** (<nav>, <header>, <footer>, <aside>, and any related links).
                        * **Advertisements/Widgets:** (Elements with class/id containing 'ad', 'banner', 'widget', 'sidebar', 'float', 'promo', or similar commercial/structural terms).
                        * **Interactive Elements:** (Comments, login forms, sharing buttons, related articles lists).
                    2.  **Extraction:**
                        * Identify the definitive **main article Title**.
                        * Generate a concise **Summary** (3-4 sentences max) of the article's core content, ensuring it is derived only from the extracted Content.
                        * Extract the full, clean **Content** (body text). Convert the HTML paragraphs (`<p>`, `<div>`s containing text) into clean, plain text, preserving paragraph breaks (use `\n\n` between paragraphs).
                    3.  **Format Adherence:** **Respond ONLY with a valid JSON object** that strictly adheres to the following structure. Do not include any introductory text, explanation, or commentary.

                     **JSON Format Requirement:**
                         {{
                           ""Title"": ""..."",
                           ""Summary"": ""..."",
                           ""Content"": ""...""
                         }}

                    **Input HTML to Process:**
                        {cleanText}";
                    //{cleanText}";
            try
            {
                // 执行 LLM 策略
                var newsContent = await _llmRetryPolicy.ExecuteAsync(async () =>
                {                    
                    
                    var chatClient = new OllamaApiClient(_ollamaUrl, _modelName);
                    var llmResponse = await chatClient.GetResponseAsync<NewsContent>(prompt);
                    return llmResponse.Result;
                    //return new NewsContent();
                });

                return newsContent;
            }catch(Exception ex)
            {
                // **重点：在这里记录详细的异常信息，这是代码没有到达 return 的原因**
                Debug.WriteLine($"Error executing LLM policy or parsing result: {ex.Message}");
                Debug.WriteLine($"Stack Trace: {ex.StackTrace}");

                // 可以选择返回一个默认值，或者重新抛出异常
                throw;
            }
        }
    }
}