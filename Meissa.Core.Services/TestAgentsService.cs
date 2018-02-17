﻿// <copyright file="TestAgentsService.cs" company="Automate The Planet Ltd.">
// Copyright 2018 Automate The Planet Ltd.
// Licensed under the Apache License, Version 2.0 (the "License");
// You may not use this file except in compliance with the License.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>
// <author>Anton Angelov</author>
// <site>https://automatetheplanet.com/</site>
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Meissa.API.Models;
using Meissa.Core.Contracts;
using Meissa.Core.Model;

namespace Meissa.Core.Services
{
    public class TestAgentsService : ITestAgentsService
    {
        private readonly IServiceClient<TestAgentDto> _testAgentRepository;

        public TestAgentsService(IServiceClient<TestAgentDto> testAgentRepository) => _testAgentRepository = testAgentRepository;

        public async Task<List<TestAgentDto>> GetAllActiveTestAgentsByTagAsync(string tag)
        {
            var testAgents = (await _testAgentRepository.GetAllAsync()).Where(x => x.AgentTag.Contains(tag) && (x.Status == TestAgentStatus.Active || x.Status == TestAgentStatus.RunningTests));

            return testAgents.ToList();
        }

        public async Task VerifyActiveStatusAsync(string agentTag)
        {
            if (await IsTestAgentAskedToVerifyActiveStatusAsync(agentTag))
            {
                var testAgent = (await _testAgentRepository.GetAllAsync()).SingleOrDefault(x => x.MachineName == Environment.MachineName && x.AgentTag == agentTag);
                testAgent.Status = TestAgentStatus.Active;
                await _testAgentRepository.UpdateAsync(testAgent.TestAgentId, testAgent);
            }
        }

        public async Task SetAllActiveAgentsToVerifyTheirStatusAsync(string tag)
        {
            var testAgents = await GetAllActiveTestAgentsByTagAsync(tag);
            if (testAgents.Count > 0)
            {
                await UpdateAgentsStatusAsync(testAgents, TestAgentStatus.RequestActiveConfirmation);
            }
        }

        public async Task WaitAllActiveAgentsToVerifyTheirStatusAsync(List<TestAgentDto> activeTestAgents)
        {
            if (activeTestAgents.Count > 0)
            {
                SpinWait.SpinUntil(() =>
                    {
                        var testAgents = GetAllActiveTestAgentsByTagAsync(activeTestAgents.FirstOrDefault()?.AgentTag).Result;
                        if (testAgents.Count(x => activeTestAgents.Any(y => y.TestAgentId.Equals(x.TestAgentId))) == activeTestAgents.Count)
                        {
                            return true;
                        }

                        Thread.Sleep(1000);

                        return false;
                    },
                    TimeSpan.FromSeconds(30));
            }
        }

        public async Task<bool> IsTestAgentActive(string agentTag)
        {
            var isActive = (await _testAgentRepository.GetAllAsync()).Any(x => x.MachineName == Environment.MachineName && x.AgentTag == agentTag && x.Status == TestAgentStatus.Active);

            return isActive;
        }

        private async Task<bool> IsTestAgentAskedToVerifyActiveStatusAsync(string agentTag)
        {
            var currentTestAgent = (await _testAgentRepository.GetAllAsync()).SingleOrDefault(x => x.MachineName == Environment.MachineName && x.AgentTag == agentTag && x.Status == TestAgentStatus.RequestActiveConfirmation);

            return currentTestAgent != null;
        }

        private async Task UpdateAgentsStatusAsync(List<TestAgentDto> testAgents, TestAgentStatus testAgentStatus)
        {
            foreach (var currentTestAgent in testAgents)
            {
                currentTestAgent.Status = testAgentStatus;
                await _testAgentRepository.UpdateAsync(currentTestAgent.TestAgentId, currentTestAgent);
            }
        }
    }
}