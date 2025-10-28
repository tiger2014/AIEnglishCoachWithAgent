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
        public OllamaAgent(string modelName, string url = "http://localhost:11434")
        {
            IChatClient client = new OllamaApiClient(url, modelName);
            _agent = new ChatClientAgent(
                        client,
                        instructions: "Your nmae is Stone. You are a spoken English practice partner. Please use only very simple vocabulary and short sentences. Your goal is to help the user have daily conversations in an easy-to-understand way. Please keep the conversation simple and direct. My name is David",
                        name: "Coach"
                        );                        
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
