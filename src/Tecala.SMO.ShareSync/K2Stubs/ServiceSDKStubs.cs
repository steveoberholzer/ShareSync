// TEMPORARY STUBS FOR K2 ServiceSDK
// Remove this file when building with actual K2 ServiceSDK installed

#if !HAS_K2_SDK

using System;
using System.Collections.Generic;

namespace SourceCode.SmartObjects.Services.ServiceSDK
{
    public abstract class ServiceAssemblyBase
    {
        protected Service Service { get; set; } = new Service();
        protected ServicePackage ServicePackage { get; set; } = new ServicePackage();

        public virtual string GetConfigSection() => string.Empty;
        public virtual string DescribeSchema() => string.Empty;
        public virtual void Extend() { }
    }

    public class Service
    {
        public ServiceConfiguration ServiceConfiguration { get; set; } = new ServiceConfiguration();
        public ServiceObjects ServiceObjects { get; set; } = new ServiceObjects();
        public string Name { get; set; }
        public ServiceMetaData MetaData { get; set; } = new ServiceMetaData();
    }

    public class ServicePackage
    {
        public bool IsSuccessful { get; set; }
        public ServiceMessages ServiceMessages { get; set; } = new ServiceMessages();
    }

    public class ServiceMessages
    {
        public void Add(string message, MessageSeverity severity)
        {
            Console.WriteLine($"[{severity}] {message}");
        }
    }

    public enum MessageSeverity
    {
        Information,
        Warning,
        Error
    }

    public class ServiceMetaData
    {
        public string DisplayName { get; set; }
        public string Description { get; set; }
    }

    public class ServiceObjects
    {
        public void Create(Objects.ServiceObject serviceObject)
        {
            Console.WriteLine($"Created service object: {serviceObject}");
        }
    }

    public class ServiceConfiguration : Dictionary<string, object>
    {
        public void Add(string key, bool required, object defaultValue)
        {
            this[key] = defaultValue;
        }
    }
}

namespace SourceCode.SmartObjects.Services.ServiceSDK.Objects
{
    public class ServiceObject
    {
        public ServiceObject(Type type)
        {
            Console.WriteLine($"ServiceObject created for type: {type.Name}");
        }
    }
}

namespace SourceCode.SmartObjects.Services.ServiceSDK.Types
{
    public enum SoType
    {
        Text,
        Memo,
        Number,
        Decimal,
        YesNo,
        DateTime,
        File,
        Guid,
        AutoGuid
    }

    public enum MethodType
    {
        Read,
        Create,
        Update,
        Delete,
        Execute,
        List
    }
}

namespace SourceCode.SmartObjects.Services.ServiceSDK.Attributes
{
    [AttributeUsage(AttributeTargets.Class)]
    public class ServiceObjectAttribute : Attribute
    {
        public ServiceObjectAttribute(string name, string displayName, string description)
        {
            Name = name;
            DisplayName = displayName;
            Description = description;
        }

        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class MethodAttribute : Attribute
    {
        public MethodAttribute(
            string name,
            Types.MethodType methodType,
            string displayName,
            string description,
            string[] requiredProperties,
            string[] inputProperties,
            string[] returnProperties)
        {
            Name = name;
            MethodType = methodType;
            DisplayName = displayName;
            Description = description;
        }

        public string Name { get; set; }
        public Types.MethodType MethodType { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class PropertyAttribute : Attribute
    {
        public PropertyAttribute(string name, Types.SoType type, string displayName, string description)
        {
            Name = name;
            Type = type;
            DisplayName = displayName;
            Description = description;
        }

        public string Name { get; set; }
        public Types.SoType Type { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
    }
}

#endif
