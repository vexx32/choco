using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Text;
using System.Threading.Tasks;

namespace Chocolatey.PowerShell.Helpers
{
    /// <summary>
    /// Lightweight "dependency injection" container that is initialized when the process is loaded.
    /// </summary>
    public static class Container
    {
        private static readonly ConcurrentDictionary<Type, object> _initializers = new ConcurrentDictionary<Type, object>();

        static Container()
        {
            // Register default initializers here.
            Register<IChecksumValidator>((cmdlet) => new ChecksumValidator(cmdlet));
        }

        private static void Register<T>(Func<PSCmdlet, T> initializer)
        {
            var result = _initializers.GetOrAdd(typeof(T), initializer);
            if (!result.Equals(initializer))
            {
                throw new NotSupportedException($"An implementation of type {typeof(T).Name} has already been registered. Use the {nameof(Override)} method to override an initializer.");
            }
        }

        public static void Override<T>(Func<PSCmdlet, T> initializer)
        {
            _initializers.AddOrUpdate(typeof(T), initializer, (key, oldValue) => initializer);
        }

        /// <summary>
        /// Initialize an instance of <typeparamref name="T"/> by providing the <see cref="PSCmdlet"/> needed to initialize it and return the resulting <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="cmdlet"></param>
        /// <returns>An implementation of type <typeparamref name="T"/> for the given <paramref name="cmdlet"/>.</returns>
        /// <exception cref="NotImplementedException">Throws if there is not a valid initializer registered for the given type.</exception>
        public static T GetInstance<T>(PSCmdlet cmdlet)
        {
            if (_initializers.TryGetValue(typeof(T), out var initializer) && initializer is Func<PSCmdlet, T> init)
            {
                return init(cmdlet);
            }

            throw new NotImplementedException($"An initializer for type ${typeof(T).Name} has not been registered. Ensure a default initializer has been registered in the {nameof(Container)} class.");
        }
    }
}
