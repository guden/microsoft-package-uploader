﻿// Copyright (C) Microsoft. All rights reserved.

using GameStoreBroker.Application.Schema;
using GameStoreBroker.ClientApi.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace GameStoreBroker.Application.Commands
{
    internal abstract class CommandAction
    {
        private readonly Options _options;
        private readonly ILogger<CommandAction> _logger;

        protected CommandAction(IHost host, Options options)
        {
            _options = options;
            _logger = host.Services.GetRequiredService<ILogger<CommandAction>>();
        }

        protected async Task<T> GetSchemaAsync<T>(CancellationToken ct) where T : BaseOperationSchema
        {
            if (!_options.ConfigFile.Exists)
            {
                throw new FileNotFoundException("ConfigFile does not exist.", _options.ConfigFile.FullName);
            }

            var schema = await DeserializeJsonFileAsync<T>(_options.ConfigFile.FullName, ct).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(_options.ClientSecret))
            {
                schema.AadAuthInfo.ClientSecret = _options.ClientSecret;
            }

            return schema;
        }

        protected static AadAuthInfo GetAadAuthInfo(AadAuthInfoSchema aadAuthInfoSchema)
        {
            if (aadAuthInfoSchema == null)
            {
                throw new ArgumentNullException(nameof(aadAuthInfoSchema));
            }

            var aadAuthInfo = new AadAuthInfo
            {
                TenantId = aadAuthInfoSchema.TenantId,
                ClientId = aadAuthInfoSchema.ClientId,
                ClientSecret = aadAuthInfoSchema.ClientSecret,
            };
            return aadAuthInfo;
        }

        public async Task<int> RunAsync(CancellationToken ct)
        {
            try
            {
                _logger.LogDebug("GameStoreBroker is running.");
                await ProcessAsync(ct).ConfigureAwait(false);
                _logger.LogInformation("GameStoreBroker has finished running.");
                return 0;
            }
            catch (TaskCanceledException)
            {
                _logger.LogWarning("Operation cancelled.");
                return 1;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Exception thrown.");
                return 1;
            }
        }

        protected abstract Task ProcessAsync(CancellationToken ct);

        private static async Task<T> DeserializeJsonFileAsync<T>(string fileName, CancellationToken ct)
        {
            if (fileName == null)
            {
                throw new ArgumentNullException(nameof(fileName));
            }

            await using var openStream = File.OpenRead(fileName);
            var deserializedObject = await JsonSerializer.DeserializeAsync<T>(openStream, cancellationToken: ct).ConfigureAwait(false);
            return deserializedObject;
        }
    }
}