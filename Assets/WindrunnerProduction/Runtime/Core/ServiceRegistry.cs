using System;
using System.Collections.Generic;

namespace Windrunner.Production.Core
{
    /// <summary>
    /// Explicitly composed, project-scoped services. A service can be registered
    /// under several interfaces, but is disposed only once at shutdown.
    /// </summary>
    public sealed class ServiceRegistry : IDisposable
    {
        private readonly Dictionary<Type, object> services = new Dictionary<Type, object>();
        private readonly List<object> ownedInstances = new List<object>();
        private bool disposed;

        public void Register<T>(T instance) where T : class
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(ServiceRegistry));
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            Type key = typeof(T);
            if (services.ContainsKey(key))
                throw new InvalidOperationException("Service already registered: " + key.FullName);

            services.Add(key, instance);

            for (int i = 0; i < ownedInstances.Count; i++)
            {
                if (ReferenceEquals(ownedInstances[i], instance))
                    return;
            }

            ownedInstances.Add(instance);
        }

        public bool TryGet<T>(out T instance) where T : class
        {
            if (disposed)
            {
                instance = null;
                return false;
            }

            object found;
            if (services.TryGetValue(typeof(T), out found))
            {
                instance = (T)found;
                return true;
            }

            instance = null;
            return false;
        }

        public T Get<T>() where T : class
        {
            T result;
            if (TryGet(out result))
                return result;

            throw new KeyNotFoundException("Service not registered: " + typeof(T).FullName);
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            List<Exception> failures = null;
            for (int i = ownedInstances.Count - 1; i >= 0; i--)
            {
                IDisposable disposable = ownedInstances[i] as IDisposable;
                if (disposable != null)
                {
                    try { disposable.Dispose(); }
                    catch (Exception exception)
                    {
                        if (failures == null) failures = new List<Exception>();
                        failures.Add(exception);
                    }
                }
            }

            ownedInstances.Clear();
            services.Clear();

            if (failures != null)
                throw new AggregateException("One or more project services failed during teardown.", failures);
        }
    }
}
