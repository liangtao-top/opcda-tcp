using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace OpcDAToMSA.DependencyInjection
{
    /// <summary>
    /// 服务生命周期枚举
    /// </summary>
    public enum ServiceLifetime
    {
        Singleton,    // 单例
        Transient,    // 瞬时
        Scoped       // 作用域
    }

    /// <summary>
    /// 服务注册信息
    /// </summary>
    public class ServiceRegistration
    {
        public Type ServiceType { get; set; }
        public Type ImplementationType { get; set; }
        public ServiceLifetime Lifetime { get; set; }
        public object Instance { get; set; }
        public Func<IServiceProvider, object> Factory { get; set; }
    }

    /// <summary>
    /// 服务提供者接口
    /// </summary>
    public interface IServiceProvider
    {
        T GetService<T>();
        object GetService(Type serviceType);
        IEnumerable<T> GetServices<T>();
        IEnumerable<object> GetServices(Type serviceType);
    }

    /// <summary>
    /// 服务容器接口
    /// </summary>
    public interface IServiceContainer
    {
        IServiceContainer Register<TInterface, TImplementation>(ServiceLifetime lifetime = ServiceLifetime.Transient);
        IServiceContainer Register<TInterface>(Func<IServiceProvider, TInterface> factory, ServiceLifetime lifetime = ServiceLifetime.Transient);
        IServiceContainer RegisterInstance<TInterface>(TInterface instance);
        IServiceProvider BuildServiceProvider();
    }

    /// <summary>
    /// 简单的依赖注入容器实现
    /// </summary>
    public class ServiceContainer : IServiceContainer
    {
        private readonly Dictionary<Type, ServiceRegistration> _registrations = new Dictionary<Type, ServiceRegistration>();
        private readonly Dictionary<Type, List<ServiceRegistration>> _multipleRegistrations = new Dictionary<Type, List<ServiceRegistration>>();

        public IServiceContainer Register<TInterface, TImplementation>(ServiceLifetime lifetime = ServiceLifetime.Transient)
        {
            var registration = new ServiceRegistration
            {
                ServiceType = typeof(TInterface),
                ImplementationType = typeof(TImplementation),
                Lifetime = lifetime
            };

            if (_registrations.ContainsKey(typeof(TInterface)))
            {
                if (!_multipleRegistrations.ContainsKey(typeof(TInterface)))
                {
                    _multipleRegistrations[typeof(TInterface)] = new List<ServiceRegistration> { _registrations[typeof(TInterface)] };
                }
                _multipleRegistrations[typeof(TInterface)].Add(registration);
            }
            else
            {
                _registrations[typeof(TInterface)] = registration;
            }

            return this;
        }

        public IServiceContainer Register<TInterface>(Func<IServiceProvider, TInterface> factory, ServiceLifetime lifetime = ServiceLifetime.Transient)
        {
            var registration = new ServiceRegistration
            {
                ServiceType = typeof(TInterface),
                Factory = provider => factory(provider),
                Lifetime = lifetime
            };

            if (_registrations.ContainsKey(typeof(TInterface)))
            {
                if (!_multipleRegistrations.ContainsKey(typeof(TInterface)))
                {
                    _multipleRegistrations[typeof(TInterface)] = new List<ServiceRegistration> { _registrations[typeof(TInterface)] };
                }
                _multipleRegistrations[typeof(TInterface)].Add(registration);
            }
            else
            {
                _registrations[typeof(TInterface)] = registration;
            }

            return this;
        }

        public IServiceContainer RegisterInstance<TInterface>(TInterface instance)
        {
            var registration = new ServiceRegistration
            {
                ServiceType = typeof(TInterface),
                Instance = instance,
                Lifetime = ServiceLifetime.Singleton
            };

            _registrations[typeof(TInterface)] = registration;
            return this;
        }

        public IServiceProvider BuildServiceProvider()
        {
            return new ServiceProvider(_registrations, _multipleRegistrations);
        }
    }

    /// <summary>
    /// 服务提供者实现
    /// </summary>
    public class ServiceProvider : IServiceProvider
    {
        private readonly Dictionary<Type, ServiceRegistration> _registrations;
        private readonly Dictionary<Type, List<ServiceRegistration>> _multipleRegistrations;
        private readonly Dictionary<Type, object> _singletons = new Dictionary<Type, object>();
        private readonly object _lock = new object();

        public ServiceProvider(
            Dictionary<Type, ServiceRegistration> registrations,
            Dictionary<Type, List<ServiceRegistration>> multipleRegistrations)
        {
            _registrations = registrations;
            _multipleRegistrations = multipleRegistrations;
        }

        public T GetService<T>()
        {
            return (T)GetService(typeof(T));
        }

        public object GetService(Type serviceType)
        {
            if (_registrations.TryGetValue(serviceType, out var registration))
            {
                return CreateInstance(registration);
            }

            // 尝试自动注册
            if (serviceType.IsClass && !serviceType.IsAbstract)
            {
                return CreateInstance(new ServiceRegistration
                {
                    ServiceType = serviceType,
                    ImplementationType = serviceType,
                    Lifetime = ServiceLifetime.Transient
                });
            }

            throw new InvalidOperationException($"Service of type {serviceType.Name} is not registered.");
        }

        public IEnumerable<T> GetServices<T>()
        {
            return GetServices(typeof(T)).Cast<T>();
        }

        public IEnumerable<object> GetServices(Type serviceType)
        {
            var services = new List<object>();

            if (_registrations.TryGetValue(serviceType, out var registration))
            {
                services.Add(CreateInstance(registration));
            }

            if (_multipleRegistrations.TryGetValue(serviceType, out var multipleRegistrations))
            {
                foreach (var reg in multipleRegistrations)
                {
                    services.Add(CreateInstance(reg));
                }
            }

            return services;
        }

        private object CreateInstance(ServiceRegistration registration)
        {
            // 单例检查
            if (registration.Lifetime == ServiceLifetime.Singleton)
            {
                lock (_lock)
                {
                    if (_singletons.TryGetValue(registration.ServiceType, out var singleton))
                    {
                        return singleton;
                    }
                }
            }

            object instance;

            // 使用工厂方法
            if (registration.Factory != null)
            {
                instance = registration.Factory(this);
            }
            // 使用已存在的实例
            else if (registration.Instance != null)
            {
                instance = registration.Instance;
            }
            // 创建新实例
            else
            {
                instance = CreateInstance(registration.ImplementationType);
            }

            // 缓存单例
            if (registration.Lifetime == ServiceLifetime.Singleton)
            {
                lock (_lock)
                {
                    _singletons[registration.ServiceType] = instance;
                }
            }

            return instance;
        }

        private object CreateInstance(Type type)
        {
            var constructors = type.GetConstructors()
                .OrderByDescending(c => c.GetParameters().Length)
                .ToArray();

            foreach (var constructor in constructors)
            {
                try
                {
                    var parameters = constructor.GetParameters();
                    var args = new object[parameters.Length];

                    for (int i = 0; i < parameters.Length; i++)
                    {
                        args[i] = GetService(parameters[i].ParameterType);
                    }

                    return Activator.CreateInstance(type, args);
                }
                catch
                {
                    // 尝试下一个构造函数
                    continue;
                }
            }

            throw new InvalidOperationException($"Cannot create instance of type {type.Name}. No suitable constructor found.");
        }
    }

    /// <summary>
    /// 服务定位器（全局访问点）
    /// </summary>
    public static class ServiceLocator
    {
        private static IServiceProvider _serviceProvider;

        public static void SetServiceProvider(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public static T GetService<T>()
        {
            if (_serviceProvider == null)
            {
                throw new InvalidOperationException("ServiceProvider is not initialized. Call SetServiceProvider first.");
            }
            return _serviceProvider.GetService<T>();
        }

        public static object GetService(Type serviceType)
        {
            if (_serviceProvider == null)
            {
                throw new InvalidOperationException("ServiceProvider is not initialized. Call SetServiceProvider first.");
            }
            return _serviceProvider.GetService(serviceType);
        }
    }
}
