using SourceCode.SmartObjects.Services.ServiceSDK;
using SourceCode.SmartObjects.Services.ServiceSDK.Objects;
using SourceCode.SmartObjects.Services.ServiceSDK.Types;
using System;
using Tecala.SMO.ShareSync.Services;

namespace Tecala.SMO.ShareSync
{
    /// <summary>
    /// K2 Service Broker for SharePoint Permission Sync
    /// </summary>
    public class ShareSyncBroker : ServiceAssemblyBase
    {
        private ServiceConfiguration _serviceConfig;

        public ShareSyncBroker()
        {
            _serviceConfig = new ServiceConfiguration();
        }

        public ServiceConfiguration ServiceConfiguration
        {
            get => _serviceConfig;
            set => _serviceConfig = value;
        }

        /// <summary>
        /// Define configuration settings for the broker
        /// </summary>
        public override string GetConfigSection()
        {
            // Database configuration
            Service.ServiceConfiguration.Add(
                "SQL Connection String",
                true,
                "Server=localhost;Database=ScyneShareDEV;Trusted_Connection=True;TrustServerCertificate=True;");

            // RabbitMQ configuration
            Service.ServiceConfiguration.Add("RabbitMQ Host", true, "localhost");
            Service.ServiceConfiguration.Add("RabbitMQ Port", true, "5672");
            Service.ServiceConfiguration.Add("RabbitMQ Username", true, "guest");
            Service.ServiceConfiguration.Add("RabbitMQ Password", true, "guest");
            Service.ServiceConfiguration.Add("RabbitMQ VirtualHost", true, "/");

            // Queue names
            Service.ServiceConfiguration.Add(
                "Queue InteractionPermissions",
                true,
                "sharepoint.interaction.permissions");
            Service.ServiceConfiguration.Add(
                "Queue InteractionCreation",
                true,
                "sharepoint.interaction.creation");
            Service.ServiceConfiguration.Add(
                "Queue RemovePermissions",
                true,
                "sharepoint.remove.permissions");

            return base.GetConfigSection();
        }

        /// <summary>
        /// Describe the service schema (service objects and methods)
        /// </summary>
        public override string DescribeSchema()
        {
            try
            {
                Service.ServiceObjects.Create(new ServiceObject(typeof(ShareSyncService)));
                Service.Name = "Tecala.SMO.ShareSync";
                Service.MetaData.DisplayName = "Tecala SharePoint Permission Sync";
                Service.MetaData.Description = "K2 broker for SharePoint permission synchronization and interaction management.";
                ServicePackage.IsSuccessful = true;
            }
            catch (Exception ex)
            {
                ServicePackage.ServiceMessages.Add(ex.Message, MessageSeverity.Error);
                ServicePackage.IsSuccessful = false;
            }

            return base.DescribeSchema();
        }

        public override void Extend()
        {
            // No extensions needed
        }
    }
}
