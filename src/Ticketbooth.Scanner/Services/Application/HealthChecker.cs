﻿using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;
using Ticketbooth.Scanner.Eventing.Args;
using Ticketbooth.Scanner.Services.Infrastructure;

namespace Ticketbooth.Scanner.Services.Application
{
    public class HealthChecker : IHealthChecker
    {
        public const string HealthyNodeState = "Started";
        public const string HeathlyFeatureState = "Initialized";
        public static readonly string[] RequiredFeatures = new string[]
        {
            "Stratis.Bitcoin.Base.BaseFeature",
            "Stratis.Bitcoin.Features.SmartContracts.SmartContractFeature",
            "Stratis.Bitcoin.Features.SmartContracts.Wallet.SmartContractWalletFeature",
            "Stratis.Bitcoin.Features.Api.ApiFeature"
        };

        private readonly INodeService _nodeService;
        private readonly ILogger<HealthChecker> _logger;

        private string _nodeAddress;
        private bool _isConnected;
        private bool _isValid;

        public event EventHandler<PropertyChangedEventArgs> OnPropertyChanged;

        public HealthChecker(INodeService nodeService, ILogger<HealthChecker> logger)
        {
            _nodeService = nodeService;
            _logger = logger;
            _isValid = true;
        }

        public bool IsConnected
        {
            get => _isConnected;
            set
            {
                if (_isConnected != value)
                {
                    _isConnected = value;
                    _logger.LogInformation($"{(_isConnected ? "Connected to" : "Disconnected from")} node at {_nodeAddress}");
                    OnPropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsConnected)));
                }
            }
        }

        public bool IsValid
        {
            get => _isValid;
            private set
            {
                if (_isValid != value)
                {
                    _isValid = value;
                    if (_isValid)
                    {
                        _logger.LogInformation($"Node at {_nodeAddress} is valid");
                    }
                    else
                    {
                        _logger.LogWarning($"Node at {_nodeAddress} does not have sufficient features");
                    }
                    OnPropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsValid)));
                }
            }
        }

        public bool IsAvailable => IsConnected && IsValid;

        public string NodeVersion { get; private set; }

        public async Task UpdateNodeHealthAsync()
        {
            var nodeStatus = await _nodeService.CheckNodeStatus();
            if (nodeStatus is null)
            {
                IsConnected = false;
                _nodeAddress = null;
                NodeVersion = null;
                return;
            }

            _nodeAddress = nodeStatus.ExternalAddress;
            NodeVersion = nodeStatus.Version;

            var requiredFeaturesAvailable = nodeStatus.FeaturesData.Where(feature => RequiredFeatures.Contains(feature.Namespace));
            IsValid = requiredFeaturesAvailable.Count() == RequiredFeatures.Length
                && requiredFeaturesAvailable.All(feature => feature.State == HeathlyFeatureState);
            IsConnected = nodeStatus.State == HealthyNodeState;
        }
    }
}