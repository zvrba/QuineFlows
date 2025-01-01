using System;
using System.Threading.Tasks;

using Autofac;

using Quine.HRCatalog;
using Quine.Schemas.Graph;

namespace Quine.Graph
{
    /// <summary>
    /// Transform node has at least one input and zero outputs.  It calls <see cref="Process(TInput)"/>
    /// method for each received message.  The node exits when all of its predecessors have exited and closes
    /// all output channels.
    /// </summary>
    public abstract class TransformNode<TState, TInput> : NodeShell<TState>
        where TState : TransformNodeState<TInput>
        where TInput : GraphMessage
    {
        public readonly InputPort<TInput> Input0;

        protected TransformNode(ILifetimeScope lifetimeScope, GraphShell owner, TState state) : base(lifetimeScope, owner, state) {
            Input0 = new InputPort<TInput>(this, State.Input0);
        }

        /// <summary>
        /// Repeatedly dequeues a message from <see cref="Input0"/> and invokes <see cref="ProcessSingleMessageAsync(TInput)"/> on it. 
        /// </summary>
        /// <exception cref="ChannelClosedException">
        /// Signals "orderly" exit from the message loop, i.e., the input channel is empty an nothing more will be enqueued.
        /// </exception>
        protected override async Task MessageLoopAsync() {
        loop:   // "Cleanly" ended by Dequeue() throwing ChannelClosedException or OperationCanceledException
            var message = QHEnsure.NotNull(await Input0.Dequeue(CancellationToken));
            await ProcessSingleMessageAsync(message);
            goto loop;
        }

        /// <summary>
        /// Wraps <see cref="ProcessAsync(TInput)"/> with raising begin/end events and exception handling.
        /// </summary>
        protected async Task ProcessSingleMessageAsync(TInput message) {
            try {
                RaiseProcessingBeginEvent(message);
                await ProcessAsync(message);
                RaiseProcessingEndEvent(MessageProcessingState.Completed, null);
            }
            catch (Exception e) {
                RaiseProcessingEndEvent(MessageProcessingState.Failed, e);
            }
        }

        /// <summary>
        /// Must be overridden in derived classes to process the message.
        /// Processing errors are signaled by exceptions.
        /// </summary>
        protected abstract Task ProcessAsync(TInput message);
    }

    /// <summary>
    /// Transform node with one output.  Arbitrary number of messages can be produced for a single input message.
    /// </summary>
    public abstract class TransformNode<TState, TInput, TOutput0> : TransformNode<TState, TInput>
        where TState : TransformNodeState<TInput, TOutput0>
        where TInput : GraphMessage
        where TOutput0 : GraphMessage
    {
        public readonly OutputPort<TOutput0> Output0;

        protected TransformNode(Autofac.ILifetimeScope lifetimeScope, GraphShell owner, TState state) : base(lifetimeScope, owner, state) {
            Output0 = new OutputPort<TOutput0>(this, State.Output0);
        }
    }

    /// <summary>
    /// Transform node with two outputs.  Arbitrary number of messages can be produced for a single input message.
    /// </summary>
    public abstract class TransformNode<TState, TInput, TOutput0, TOutput1> : TransformNode<TState, TInput, TOutput0>
        where TState : TransformNodeState<TInput, TOutput0, TOutput1>
        where TInput : GraphMessage
        where TOutput0 : GraphMessage
        where TOutput1 : GraphMessage
    {
        public readonly OutputPort<TOutput1> Output1;

        protected TransformNode(Autofac.ILifetimeScope lifetimeScope, GraphShell owner, TState state) : base(lifetimeScope, owner, state) {
            Output1 = new OutputPort<TOutput1>(this, State.Output1);
        }
    }
}
