// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;
using Castle.DynamicProxy;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Extends the client and entity contexts to support typed object-style invocations using CLR interfaces.
    /// </summary>
    public static class TypedInvocationExtensions
    {
        private static ProxyGenerator proxyGenerator = new ProxyGenerator();

        /// <summary>
        /// Signals an entity to perform an operation.
        /// </summary>
        /// <typeparam name="T">The entity interface for this operation.</typeparam>
        /// <param name="client">The client.</param>
        /// <param name="entityId">The target entity.</param>
        /// <param name="invocation">The entity invocation.</param>
        /// <param name="taskHubName">The TaskHubName of the target entity.</param>
        /// <param name="connectionName">The name of the connection string associated with <paramref name="taskHubName"/>.</param>
        /// <returns>A task that completes when the message has been reliably enqueued.</returns>
        /// <remarks>
        /// All method arguments are serialized with <see cref="BinaryFormatter"/>. User-defined
        /// types must thus be marked as serializable.
        /// </remarks>
        public static Task SignalEntityAsync<T>(this IDurableOrchestrationClient client, EntityId entityId, Action<T> invocation, string taskHubName = null, string connectionName = null)
            where T : class
        {
            var invocationInfo = new TypedInvocationInfo();
            var proxy = proxyGenerator.CreateInterfaceProxyWithoutTarget<T>(invocationInfo);
            invocation(proxy);
            return client.SignalEntityAsync(entityId, invocationInfo.Name, invocationInfo.SerializedArguments, taskHubName, connectionName);
        }

        /// <summary>
        /// Sends a signal to an entity to perform an operation. Does not wait for a response, result, or exception.
        /// </summary>
        /// <typeparam name="T">The entity interface for this operation.</typeparam>
        /// <param name="context">The context.</param>
        /// <param name="entity">The target entity.</param>
        /// <param name="invocation">The entity invocation.</param>
        /// <remarks>
        /// All method arguments are serialized with <see cref="BinaryFormatter"/>. User-defined
        /// types must thus be marked as serializable.
        /// </remarks>
        public static void SignalEntity<T>(this IDeterministicExecutionContext context, EntityId entity, Action<T> invocation)
            where T : class
        {
            var invocationInfo = new TypedInvocationInfo();
            var proxy = proxyGenerator.CreateInterfaceProxyWithoutTarget<T>(invocationInfo);
            invocation(proxy);
            context.SignalEntity(entity, invocationInfo.Name, invocationInfo.SerializedArguments);
        }

        /// <summary>
        /// Calls an operation on an entity, passing an argument, and returns the result asynchronously.
        /// </summary>
        /// <typeparam name="T">The entity interface for this operation.</typeparam>
        /// <param name="context">The context.</param>
        /// <param name="entityId">The target entity.</param>
        /// <param name="invocation">The invocation.</param>
        /// <returns>A task representing the completion of the operation on the entity.</returns>
        /// <exception cref="LockingRulesViolationException">if the context already holds some locks, but not the one for <paramref name="entityId"/>.</exception>
        /// <remarks>
        /// All method arguments are serialized with <see cref="BinaryFormatter"/>. User-defined
        /// types must thus be marked as serializable.
        /// </remarks>
        public static Task CallEntityAsync<T>(this IInterleavingContext context, EntityId entityId, Action<T> invocation)
            where T : class
        {
            var invocationInfo = new TypedInvocationInfo();
            var proxy = proxyGenerator.CreateInterfaceProxyWithoutTarget<T>(invocationInfo);
            invocation(proxy);
            return context.CallEntityAsync(entityId, invocationInfo.Name, invocationInfo.SerializedArguments);
        }

        /// <summary>
        /// Calls an operation on an entity, passing an argument, and returns the result asynchronously.
        /// </summary>
        /// <typeparam name="TEntityInterface">The entity interface for this operation.</typeparam>
        /// <typeparam name="TResult">The JSON-serializable result type of the operation.</typeparam>
        /// <param name="context">The context.</param>
        /// <param name="entityId">The target entity.</param>
        /// <param name="invocation">The invocation.</param>
        /// <returns>A task representing the result of the operation.</returns>
        /// <exception cref="LockingRulesViolationException">if the context already holds some locks, but not the one for <paramref name="entityId"/>.</exception>
        /// <remarks>
        /// All method arguments are serialized with <see cref="BinaryFormatter"/>. User-defined
        /// types must thus be marked as serializable.
        /// </remarks>
        public static Task<TResult> CallEntityAsync<TEntityInterface, TResult>(this IInterleavingContext context, EntityId entityId, Func<TEntityInterface, TResult> invocation)
             where TEntityInterface : class
        {
            var invocationInfo = new TypedInvocationInfo();
            var proxy = proxyGenerator.CreateInterfaceProxyWithoutTarget<TEntityInterface>(invocationInfo);
            invocation(proxy);
            return context.CallEntityAsync<TResult>(entityId, invocationInfo.Name, invocationInfo.SerializedArguments);
        }

        /// <summary>
        /// Calls an operation on an entity, passing an argument, and returns the result asynchronously.
        /// </summary>
        /// <typeparam name="TEntityInterface">The entity interface for this operation.</typeparam>
        /// <typeparam name="TResult">The JSON-serializable result type of the operation.</typeparam>
        /// <param name="context">The context.</param>
        /// <param name="entityId">The target entity.</param>
        /// <param name="invocation">The invocation.</param>
        /// <returns>A task representing the result of the operation.</returns>
        /// <exception cref="LockingRulesViolationException">if the context already holds some locks, but not the one for <paramref name="entityId"/>.</exception>
        /// <remarks>
        /// All method arguments are serialized with <see cref="BinaryFormatter"/>. User-defined
        /// types must thus be marked as serializable.
        /// </remarks>
        public static Task<TResult> CallEntityAsync<TEntityInterface, TResult>(this IInterleavingContext context, EntityId entityId, Func<TEntityInterface, Task<TResult>> invocation)
             where TEntityInterface : class
        {
            var invocationInfo = new TypedInvocationInfo();
            var proxy = proxyGenerator.CreateInterfaceProxyWithoutTarget<TEntityInterface>(invocationInfo);
            invocation(proxy);
            return context.CallEntityAsync<TResult>(entityId, invocationInfo.Name, invocationInfo.SerializedArguments);
        }

        /// <summary>
        /// Dynamically dispatches the incoming entity operation using reflection.
        /// </summary>
        /// <typeparam name="T">The class to use for entity instances.</typeparam>
        /// <returns>A task that completes when the dispatched operation has finished.</returns>
        /// <remarks>
        /// If the entity's state is null, an object of type <typeparamref name="T"/> is created first. Then, reflection
        /// is used to try to find a matching method. This match is based on the method name
        /// (which is the operation name) and the argument list (which is the operation content, deserialized into
        /// an object array).
        /// </remarks>
        public static async Task DispatchAsync<T>(this IDurableEntityContext context)
            where T : new()
        {
            var invocationInfo = new TypedInvocationInfo()
            {
                Name = context.OperationName,
                SerializedArguments = context.GetInput<byte[]>(),
            };

            var state = context.GetState<T>(() => new T());

            var result = await invocationInfo.Invoke(state);

            context.Return(result);
        }
    }
}
