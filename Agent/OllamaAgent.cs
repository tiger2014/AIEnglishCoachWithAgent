using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OllamaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace AIEnglishCoachWithAgent.Agent
{
    public class OllamaAgent
    {
        public AIAgent _agent;
        public OllamaAgent(string modelName, string instructions, string url = "http://localhost:11434")
        {
            // https://learn.microsoft.com/zh-cn/agent-framework/user-guide/agents/agent-observability?pivots=programming-language-csharp
            IChatClient client = new OllamaApiClient(url, modelName);

            // 不知道为什么，在 agent 上配置 OpenTelemetr 不行，只能这样
            client = client.AsBuilder().UseOpenTelemetry(sourceName: "Test", configure: (cfg) => cfg.EnableSensitiveData = true).Build();

            _agent = new ChatClientAgent(
                        client,
                        instructions: instructions,
                        name: "Coach"
                        );

            //_agent = new ChatClientAgent(
            //    client,
            //    name: "OpenTelemetryDemoAgent",
            //    instructions: "You are a helpful assistant that provides concise and informative responses.",
            //    tools: [AIFunctionFactory.Create(GetWeatherAsync)]
            //).WithOpenTelemetry(sourceName: "MyApplication", enableSensitiveData: true);    // Enable OpenTelemetry instrumentation with sensitive data
        }

        /// <summary>
        /// 生成对话 thread
        /// await agent.RunAsync("Tell me a joke about a pirate.", thread));
        /// </summary>
        /// <returns></returns>

        public AgentThread? CreateNewChatThread()
        {
            return _agent?.GetNewThread();
        }
    }
}
