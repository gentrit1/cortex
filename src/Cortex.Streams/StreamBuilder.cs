﻿using Cortex.States;
using Cortex.Streams.Abstractions;
using Cortex.Streams.Operators;
using Cortex.Telemetry;
using System;
using System.Collections.Generic;

namespace Cortex.Streams
{
    /// <summary>
    /// Builds a stream processing pipeline with optional branches.
    /// </summary>
    /// <typeparam name="TIn">The type of the initial input to the stream.</typeparam>
    /// <typeparam name="TCurrent">The current type of data in the stream.</typeparam>
    public class StreamBuilder<TIn, TCurrent> : IInitialStreamBuilder<TIn, TCurrent>, IStreamBuilder<TIn, TCurrent>
    {
        private readonly string _name;
        private IOperator _firstOperator;
        private IOperator _lastOperator;
        private bool _sourceAdded = false;
        private readonly List<BranchOperator<TCurrent>> _branchOperators = new List<BranchOperator<TCurrent>>();
        private ForkOperator<TCurrent> _forkOperator;

        private ITelemetryProvider _telemetryProvider;


        private StreamBuilder(string name)
        {
            _name = name;
        }

        private StreamBuilder(string name, IOperator firstOperator, IOperator lastOperator, bool sourceAdded)
        {
            _name = name;
            _firstOperator = firstOperator;
            _lastOperator = lastOperator;
            _sourceAdded = sourceAdded;
        }

        /// <summary>
        /// Creates a new stream with the specified name.
        /// </summary>
        /// <param name="name">The name of the stream.</param>
        /// <returns>An initial stream builder.</returns>
        public static IInitialStreamBuilder<TIn, TIn> CreateNewStream(string name)
        {
            return new StreamBuilder<TIn, TIn>(name);
        }

        /// <summary>
        /// Creates a new stream with the specified name.
        /// </summary>
        /// <param name="name">The name of the stream.</param>
        /// <param name="firstOperator">The first operator in the pipeline</param>
        /// <param name="lastOperator">The last operator in the pipeline</param>
        /// <returns>An initial stream builder.</returns>
        public static IStreamBuilder<TIn, TCurrent> CreateNewStream(string name, IOperator firstOperator, IOperator lastOperator)
        {
            return new StreamBuilder<TIn, TCurrent>(name, firstOperator, lastOperator, false);
        }

        /// <summary>
        /// Adds a map operator to the branch to transform data.
        /// </summary>
        /// <typeparam name="TNext">The type of data after the transformation.</typeparam>
        /// <param name="mapFunction">A function to transform data.</param>
        /// <returns>The branch stream builder with the new data type.</returns>
        public IStreamBuilder<TIn, TNext> Map<TNext>(Func<TCurrent, TNext> mapFunction)
        {
            var mapOperator = new MapOperator<TCurrent, TNext>(mapFunction);

            if (_firstOperator == null)
            {
                _firstOperator = mapOperator;
                _lastOperator = mapOperator;
            }
            else
            {
                _lastOperator.SetNext(mapOperator);
                _lastOperator = mapOperator;
            }

            return new StreamBuilder<TIn, TNext>(_name, _firstOperator, _lastOperator, _sourceAdded);
        }

        /// <summary>
        /// Adds a filter operator to the branch.
        /// </summary>
        /// <param name="predicate">A function to filter data.</param>
        /// <returns>The branch stream builder for method chaining.</returns>
        public IStreamBuilder<TIn, TCurrent> Filter(Func<TCurrent, bool> predicate)
        {
            var filterOperator = new FilterOperator<TCurrent>(predicate);

            if (_firstOperator == null)
            {
                _firstOperator = filterOperator;
                _lastOperator = filterOperator;
            }
            else
            {
                _lastOperator.SetNext(filterOperator);
                _lastOperator = filterOperator;
            }

            return this; // Returns the current builder for method chaining
        }

        /// <summary>
        /// Adds a sink function to the branch to consume data.
        /// </summary>
        /// <param name="sinkFunction">An action to consume data.</param>
        public ISinkBuilder<TIn, TCurrent> Sink(Action<TCurrent> sinkFunction)
        {
            var sinkOperator = new SinkOperator<TCurrent>(sinkFunction);

            if (_firstOperator == null)
            {
                _firstOperator = sinkOperator;
                _lastOperator = sinkOperator;
            }
            else
            {
                _lastOperator.SetNext(sinkOperator);
                _lastOperator = sinkOperator;
            }

            return new SinkBuilder<TIn, TCurrent>(_name, _firstOperator, _branchOperators, _telemetryProvider);
        }

        /// <summary>
        /// Adds a sink operator to the branch to consume data.
        /// </summary>
        /// <param name="sinkOperator">A sink operator to consume data.</param>
        public ISinkBuilder<TIn, TCurrent> Sink(ISinkOperator<TCurrent> sinkOperator)
        {
            var sinkAdapter = new SinkOperatorAdapter<TCurrent>(sinkOperator);

            if (_firstOperator == null)
            {
                _firstOperator = sinkAdapter;
                _lastOperator = sinkAdapter;
            }
            else
            {
                _lastOperator.SetNext(sinkAdapter);
                _lastOperator = sinkAdapter;
            }

            return new SinkBuilder<TIn, TCurrent>(_name, _firstOperator, _branchOperators, _telemetryProvider);
        }

        /// <summary>
        /// Start configuring the Stream
        /// </summary>
        /// <param name="sourceOperator">Type of the Source Operator</param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public IStreamBuilder<TIn, TCurrent> Stream(ISourceOperator<TCurrent> sourceOperator)
        {
            if (_sourceAdded)
            {
                throw new InvalidOperationException("Source operator already added.");
            }

            var sourceAdapter = new SourceOperatorAdapter<TCurrent>(sourceOperator);

            if (_firstOperator == null)
            {
                _firstOperator = sourceAdapter;
                _lastOperator = sourceAdapter;
            }
            else
            {
                throw new InvalidOperationException("Cannot add a source operator after other operators.");
            }

            _sourceAdded = true;
            return this; // Returns IStreamBuilder<TIn, TCurrent>
        }

        /// <summary>
        /// Start the stream inside the application, in-app streaming
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public IStreamBuilder<TIn, TCurrent> Stream()
        {
            // In memory source added.
            if (_sourceAdded)
            {
                throw new InvalidOperationException("Source operator already added.");
            }

            _sourceAdded = true;
            return this; // Returns IStreamBuilder<TIn, TCurrent>
        }


        public IStream<TIn, TCurrent> Build()
        {
            //return new Stream<TIn, TCurrent>(_name, _firstOperator, _branchOperators);
            return new Stream<TIn, TCurrent>(_name, _firstOperator, _branchOperators, _telemetryProvider);

        }

        /// <summary>
        /// Start creating branches, each branch can contain filtering, mapping and sink of the data
        /// </summary>
        /// <param name="name">Name of the branch</param>
        /// <param name="config">Action of configuring the branch</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        public IStreamBuilder<TIn, TCurrent> AddBranch(string name, Action<IBranchStreamBuilder<TIn, TCurrent>> config)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("Branch name cannot be null or empty.", nameof(name));
            }
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            // Initialize the fork operator if it's not already
            if (_forkOperator == null)
            {
                _forkOperator = new ForkOperator<TCurrent>();

                if (_firstOperator == null)
                {
                    _firstOperator = _forkOperator;
                    _lastOperator = _forkOperator;
                }
                else
                {
                    _lastOperator.SetNext(_forkOperator);
                    _lastOperator = _forkOperator;
                }
            }

            // Create a new branch builder
            var branchBuilder = new BranchStreamBuilder<TIn, TCurrent>(_name);
            config(branchBuilder);

            if (branchBuilder._firstOperator == null)
            {
                throw new InvalidOperationException($"Branch '{name}' must have at least one operator.");
            }

            var branchOperator = new BranchOperator<TCurrent>(name, branchBuilder._firstOperator);
            _forkOperator.AddBranch(name, branchOperator);
            _branchOperators.Add(branchOperator);

            return this;
        }

        public IStreamBuilder<TIn, TCurrent> GroupBy<TKey>(Func<TCurrent, TKey> keySelector, string stateStoreName = null, States.IStateStore<TKey, List<TCurrent>> stateStore = null)
        {
            if (stateStore == null)
            {
                if (string.IsNullOrEmpty(stateStoreName))
                {
                    stateStoreName = $"GroupByStateStore_{Guid.NewGuid()}";
                }
                stateStore = new InMemoryStateStore<TKey, List<TCurrent>>(stateStoreName);
            }

            var groupByOperator = new GroupByKeyOperator<TCurrent, TKey>(keySelector, stateStore);

            if (_firstOperator == null)
            {
                _firstOperator = groupByOperator;
                _lastOperator = groupByOperator;
            }
            else
            {
                _lastOperator.SetNext(groupByOperator);
                _lastOperator = groupByOperator;
            }

            //return new StreamBuilder<TIn, KeyValuePair<TKey, TCurrent>>(_name, _firstOperator, _lastOperator, _sourceAdded);
            return new StreamBuilder<TIn, TCurrent>(_name, _firstOperator, _lastOperator, _sourceAdded);
        }

        public IStreamBuilder<TIn, TCurrent> Aggregate<TKey, TAggregate>(Func<TCurrent, TKey> keySelector, Func<TAggregate, TCurrent, TAggregate> aggregateFunction, string stateStoreName = null, States.IStateStore<TKey, TAggregate> stateStore = null)
        {
            //private readonly Func<TInput, TKey> _keySelector
            if (stateStore == null)
            {
                if (string.IsNullOrEmpty(stateStoreName))
                {
                    stateStoreName = $"AggregateStateStore_{Guid.NewGuid()}";
                }
                stateStore = new InMemoryStateStore<TKey, TAggregate>(stateStoreName);
            }

            var aggregateOperator = new AggregateOperator<TKey, TCurrent, TAggregate>(keySelector, aggregateFunction, stateStore);

            if (_firstOperator == null)
            {
                _firstOperator = aggregateOperator;
                _lastOperator = aggregateOperator;
            }
            else
            {
                _lastOperator.SetNext(aggregateOperator);
                _lastOperator = aggregateOperator;
            }

            //return new StreamBuilder<TIn, KeyValuePair<TKey, TAggregate>>(_name, _firstOperator, _lastOperator, _sourceAdded);
            return new StreamBuilder<TIn, TCurrent>(_name, _firstOperator, _lastOperator, _sourceAdded);
        }

        public IInitialStreamBuilder<TIn, TCurrent> WithTelemetry(ITelemetryProvider telemetryProvider)
        {
            _telemetryProvider = telemetryProvider;
            return this;
        }

        /// <summary>
        /// Adds a tumbling window operator to the stream.
        /// </summary>
        /// <typeparam name="TKey">The type of the key to group by.</typeparam>
        /// <typeparam name="TWindowOutput">The type of the output after windowing.</typeparam>
        /// <param name="keySelector">A function to extract the key from data.</param>
        /// <param name="windowDuration">The duration of the tumbling window.</param>
        /// <param name="windowFunction">A function to process the data in the window.</param>
        /// <param name="stateStoreName">Optional name for the state store.</param>
        /// <param name="stateStore">Optional state store instance.</param>
        /// <returns>A stream builder with the new data type.</returns>
        public IStreamBuilder<TIn, TWindowOutput> TumblingWindow<TKey, TWindowOutput>(
            Func<TCurrent, TKey> keySelector,
            TimeSpan windowDuration,
            Func<IEnumerable<TCurrent>, TWindowOutput> windowFunction,
            string windowStateStoreName = null,
            string windowResultsStateStoreName = null,
            IStateStore<TKey, List<TCurrent>> windowStateStore = null,
            IStateStore<(TKey, DateTime), TWindowOutput> windowResultsStateStore = null)
        {
            if (windowStateStore == null)
            {
                if (string.IsNullOrEmpty(windowStateStoreName))
                {
                    windowStateStoreName = $"TumblingWindowStateStore_{Guid.NewGuid()}";
                }
                windowStateStore = new InMemoryStateStore<TKey, List<TCurrent>>(windowStateStoreName);
            }

            if (windowResultsStateStore == null && !string.IsNullOrEmpty(windowResultsStateStoreName))
            {
                windowResultsStateStore = new InMemoryStateStore<(TKey, DateTime), TWindowOutput>(windowResultsStateStoreName);
            }

            var windowOperator = new TumblingWindowOperator<TCurrent, TKey, TWindowOutput>(
                keySelector, windowDuration, windowFunction, windowStateStore, windowResultsStateStore);

            if (_firstOperator == null)
            {
                _firstOperator = windowOperator;
                _lastOperator = windowOperator;
            }
            else
            {
                _lastOperator.SetNext(windowOperator);
                _lastOperator = windowOperator;
            }

            return new StreamBuilder<TIn, TWindowOutput>(_name, _firstOperator, _lastOperator, _sourceAdded);
        }

        public IStreamBuilder<TIn, TWindowOutput> SlidingWindow<TKey, TWindowOutput>(
            Func<TCurrent, TKey> keySelector,
            TimeSpan windowSize,
            TimeSpan advanceBy,
            Func<IEnumerable<TCurrent>, TWindowOutput> windowFunction,
            string windowStateStoreName = null,
            string windowResultsStateStoreName = null,
            IStateStore<TKey, List<(TCurrent, DateTime)>> windowStateStore = null,
            IStateStore<(TKey, DateTime), TWindowOutput> windowResultsStateStore = null)
        {
            if (windowStateStore == null)
            {
                if (string.IsNullOrEmpty(windowStateStoreName))
                {
                    windowStateStoreName = $"SlidingWindowStateStore_{Guid.NewGuid()}";
                }
                windowStateStore = new InMemoryStateStore<TKey, List<(TCurrent, DateTime)>>(windowStateStoreName);
            }

            if (windowResultsStateStore == null && !string.IsNullOrEmpty(windowResultsStateStoreName))
            {
                windowResultsStateStore = new InMemoryStateStore<(TKey, DateTime), TWindowOutput>(windowResultsStateStoreName);
            }

            var windowOperator = new SlidingWindowOperator<TCurrent, TKey, TWindowOutput>(
                keySelector, windowSize, advanceBy, windowFunction, windowStateStore, windowResultsStateStore);

            if (_firstOperator == null)
            {
                _firstOperator = windowOperator;
                _lastOperator = windowOperator;
            }
            else
            {
                _lastOperator.SetNext(windowOperator);
                _lastOperator = windowOperator;
            }

            return new StreamBuilder<TIn, TWindowOutput>(_name, _firstOperator, _lastOperator, _sourceAdded);
        }

        public IStreamBuilder<TIn, TWindowOutput> SessionWindow<TKey, TWindowOutput>(
            Func<TCurrent, TKey> keySelector,
            TimeSpan inactivityGap,
            Func<IEnumerable<TCurrent>, TWindowOutput> windowFunction,
            string sessionStateStoreName = null,
            string windowResultsStateStoreName = null,
            IStateStore<TKey, SessionWindowState<TCurrent>> sessionStateStore = null,
            IStateStore<(TKey, DateTime), TWindowOutput> windowResultsStateStore = null)
        {
            if (sessionStateStore == null)
            {
                if (string.IsNullOrEmpty(sessionStateStoreName))
                {
                    sessionStateStoreName = $"SessionStateStore_{Guid.NewGuid()}";
                }
                sessionStateStore = new InMemoryStateStore<TKey, SessionWindowState<TCurrent>>(sessionStateStoreName);
            }

            if (windowResultsStateStore == null && !string.IsNullOrEmpty(windowResultsStateStoreName))
            {
                windowResultsStateStore = new InMemoryStateStore<(TKey, DateTime), TWindowOutput>(windowResultsStateStoreName);
            }

            var sessionWindowOperator = new SessionWindowOperator<TCurrent, TKey, TWindowOutput>(
                keySelector, inactivityGap, windowFunction, sessionStateStore, windowResultsStateStore);

            if (_firstOperator == null)
            {
                _firstOperator = sessionWindowOperator;
                _lastOperator = sessionWindowOperator;
            }
            else
            {
                _lastOperator.SetNext(sessionWindowOperator);
                _lastOperator = sessionWindowOperator;
            }

            return new StreamBuilder<TIn, TWindowOutput>(_name, _firstOperator, _lastOperator, _sourceAdded);
        }

        public IStreamBuilder<TIn, TCurrent> SetNext(IOperator customOperator)
        {
            if (_firstOperator == null)
            {
                _firstOperator = customOperator;
                _lastOperator = customOperator;
            }
            else
            {
                _lastOperator.SetNext(customOperator);
                _lastOperator = customOperator;
            }

            return this; // Returns the current builder for method chaining
        }
    }
}
