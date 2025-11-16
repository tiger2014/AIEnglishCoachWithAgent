using AIEnglishCoachWithAgent.Service;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OllamaSharp;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AIEnglishCoachWithAgent.Agent
{
    public class OllamaAgent
    {
        public AIAgent _agent;
        public string _agentUrl;
        private string _modelName;

        public OllamaAgent(string modelName, string instructions, string url = "http://localhost:11434")
        {
            _agentUrl = url;
            _modelName = modelName;
            // https://learn.microsoft.com/zh-cn/agent-framework/user-guide/agents/agent-observability?pivots=programming-language-csharp
            IChatClient client = new OllamaApiClient(url, modelName);

            // 不知道为什么，在 agent 上配置 OpenTelemetr 不行，只能这样
            client = client.AsBuilder().UseOpenTelemetry(sourceName: "Test", configure: (cfg) => cfg.EnableSensitiveData = true).Build();

            _agent = new ChatClientAgent(
                        client,
                        instructions: instructions,
                        name: "Coach",
                        tools: [AIFunctionFactory.Create(RequestNewsUrl)]
                        );

            //_agent = new ChatClientAgent(
            //    client,
            //    name: "OpenTelemetryDemoAgent",
            //    instructions: "You are a helpful assistant that provides concise and informative responses.",
            //    tools: [AIFunctionFactory.Create(GetWeatherAsync)]
            //).WithOpenTelemetry(sourceName: "MyApplication", enableSensitiveData: true);    // Enable OpenTelemetry instrumentation with sensitive data
        }

        // 定义事件：请求用户输入URL
        public event Func<Task<string>>? OnRequestUrlInput;

        [Description("Request user to input a news URL when they want to read news but haven't provided a link")]
        private async Task<string> RequestNewsUrl()
        {
            if (OnRequestUrlInput != null)
            {
                string url = await OnRequestUrlInput.Invoke();

                if (string.IsNullOrEmpty(url))
                    return "User cancelled URL input";

                // 获到URL后，直接调用抓取
                return await FetchNewsWrapper(url);
            }

            return "Unable to get URL input";
        }

        [Description("Fetch and analyze news content when user provides a news URL")]
        private async Task<string> FetchNewsWrapper(
        [Description("news link")] string url)
        {
            var newsService = new NewsService(_agentUrl, _modelName);
            var newsContent = await newsService.GetNewsContentAsync(url);
            return $"Title：{newsContent.Title}\nSummary：{newsContent.Summary}\nArticle：{newsContent.Content}";
        }

        public AgentThread? CreateNewChatThread()
        {
            return _agent?.GetNewThread();
        }
    }
}