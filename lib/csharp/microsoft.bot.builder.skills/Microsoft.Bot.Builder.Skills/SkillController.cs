﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Builder.Solutions;
using Newtonsoft.Json;

namespace Microsoft.Bot.Builder.Skills
{
    /// <summary>
    /// This is the default Controller that contains APIs for handling
    /// calls from a channel and calls from a parent bot (to a skill bot).
    /// </summary>
    public abstract class SkillController : ControllerBase
    {
        private readonly IBot _bot;
        private readonly IBotFrameworkHttpAdapter _botFrameworkHttpAdapter;
        private readonly SkillHttpAdapter _skillHttpAdapter;
        private readonly SkillWebSocketAdapter _skillWebSocketAdapter;
        private readonly BotSettingsBase _botSettings;
        private readonly JsonSerializer _jsonSerializer = JsonSerializer.Create(Serialization.Settings);

		public SkillController(
            IBotFrameworkHttpAdapter botFrameworkHttpAdapter,
            SkillHttpAdapter skillHttpAdapter,
            SkillWebSocketAdapter skillWebSocketAdapter,
            IBot bot,
            BotSettingsBase botSettings)
        {
            _botFrameworkHttpAdapter = botFrameworkHttpAdapter ?? throw new ArgumentNullException(nameof(IBotFrameworkHttpAdapter));
            _skillHttpAdapter = skillHttpAdapter;
            _skillWebSocketAdapter = skillWebSocketAdapter;

            _bot = bot ?? throw new ArgumentNullException(nameof(IBot));
            _botSettings = botSettings ?? throw new ArgumentNullException(nameof(botSettings));
        }

		// Each skill provides a template manifest file which we use to fill in the dynamic elements.
		// There are protected to enable unit tests to mock.
		protected HttpClient HttpClient { get; set; } = new HttpClient();

		protected string ManifestTemplateFilename { get; set; } = "manifestTemplate.json";

		/// <summary>
		/// This API is the endpoint for when a bot receives a message from a channel or a parent bot.
		/// </summary>
		/// <returns>Task.</returns>
		[Route("api/messages")]
        [HttpPost]
        public async Task BotMessage()
        {
            await _botFrameworkHttpAdapter.ProcessAsync(Request, Response, _bot);
        }

        /// <summary>
        /// This API is the endpoint the bot exposes as skill.
        /// </summary>
        /// <returns>Task.</returns>
        [Route("api/skill/messages")]
        [HttpGet]
        public async Task SkillMessageWebSocket()
        {
            if (_skillWebSocketAdapter != null)
            {
                await _skillWebSocketAdapter.ProcessAsync(Request, Response, _bot);
            }
            else
            {
                Response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
            }
        }

        /// <summary>
        /// This API is the endpoint the bot exposes as skill.
        /// </summary>
        /// <returns>Task.</returns>
        [Route("api/skill/messages")]
        [HttpPost]
        public async Task SkillMessage()
        {
            if (_skillHttpAdapter != null)
            {
                await _skillHttpAdapter.ProcessAsync(Request, Response, _bot);
            }
            else
            {
                Response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
            }
        }

        /// <summary>
        /// This API is the manifest endpoint that surfaces a self-describing document of the Skill.
        /// The base template is provided by the template and at runtime we fill in endpoint, msaAppId
        /// and as requested the triggering utterances inline within the manifest.
        /// </summary>
        /// <param name="inlineTriggerUtterances">Include triggering utterances inline in manifest.</param>
        /// <returns>Task.</returns>
        [Route("api/skill/manifest")]
        [HttpGet]
        public async Task SkillManifest([Bind, FromQuery] bool inlineTriggerUtterances = false)
        {
            try
            {
                string skillUriBase = $"{HttpContext.Request.Scheme}://{HttpContext.Request.Host}";

                SkillManifestGenerator manifestGenerator = new SkillManifestGenerator(HttpClient);
                var skillManifest = await manifestGenerator.GenerateManifest(ManifestTemplateFilename, _botSettings.MicrosoftAppId, _botSettings.CognitiveModels, skillUriBase, inlineTriggerUtterances);

                Response.ContentType = "application/json";
                Response.StatusCode = 200;

                using (var writer = new StreamWriter(Response.Body))
                {
                    using (var jsonWriter = new JsonTextWriter(writer))
                    {
                        _jsonSerializer.Serialize(jsonWriter, skillManifest);
                    }
                }
            }
            catch (Exception e)
            {
                Response.ContentType = "application/json";
                Response.StatusCode = (int)HttpStatusCode.InternalServerError;

                using (var writer = new StreamWriter(Response.Body))
                {
                    using (var jsonWriter = new JsonTextWriter(writer))
                    {
                        _jsonSerializer.Serialize(jsonWriter, e.Message);
                    }
                }
            }
        }
    }
}