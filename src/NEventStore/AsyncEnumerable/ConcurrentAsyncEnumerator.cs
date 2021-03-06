﻿using System;
using System.Threading;
using System.Threading.Tasks;

namespace ALinq
{
    internal class Signal<T>
    {
        private TaskCompletionSource<T> _tcs = new TaskCompletionSource<T>();

        public void Set(T value = default(T))
        {
            _tcs.TrySetResult(value);
        }

        public async Task<T> Wait()
        {
            var result = await _tcs.Task.ConfigureAwait(false);
            Interlocked.Exchange(ref _tcs, new TaskCompletionSource<T>());
            return result;
        }


    }

    internal sealed class ConcurrentAsyncEnumerator<T> : IAsyncEnumerator<T>, IDisposable
    {

        private readonly Func<ConcurrentAsyncEnumerator<T>, Task> _producer;
        private readonly Signal<bool> _moveNextSignal = new Signal<bool>();
        private readonly Signal<bool> _producerResult = new Signal<bool>();
        private Task _producerTask;
        private T _current;
        private bool _disposed;

        T IAsyncEnumerator<T>.Current
        {
            get { return _current; }
        }

        object IAsyncEnumerator.Current
        {
            get { return _current; }
        }

        async Task<bool> IAsyncEnumerator.MoveNext()
        {
            try
            {
                if (_producerTask == null)
                {
                    _producerTask = _producer(this);
                }

                var hasNext = await SignalProducer().ConfigureAwait(false);
                if (!hasNext)
                {
                    // Marshal exceptions and cancellation back to the consumer
                    await _producerTask.ConfigureAwait(false);
                    return false;
                }
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        private async Task<bool> SignalProducer()
        {
            _moveNextSignal.Set();
            return await _producerResult.Wait().ConfigureAwait(false);
        }

        private async Task Yield(T value)
        {
            if (!_disposed)
            {
                _current = value;
                _producerResult.Set(true);

                // Now, wait for the consumer to call MoveNext().
                // This will guarantee that _current instance is valid until
                // the consumer(s) are done.
                await _moveNextSignal.Wait().ConfigureAwait(false);
            }
        }

        private void EndOfStream()
        {
            if (!_disposed)
            {
                _producerResult.Set();
            }
        }

        public void Dispose()
        {
            _disposed = true;
            // Wake up the producer if it was stuck in Yield.
            _moveNextSignal.Set();
            // Signal the EndOfStream
            _producerResult.Set();
        }

        internal ConcurrentAsyncEnumerator(Func<ConcurrentAsyncProducer<T>, Task> producerFunc)
        {
            _producer = async enumerator =>
            {
                var producer = new ConcurrentAsyncProducer<T>(item => enumerator.Yield(item));

                try
                {
                    // Wait for the consumer to call MoveNext() the first time.
                    // Note that after the first time, this will be always signalled.
                    // This is only necessary to synchronize the enumerator when the 
                    // producer is started.
                    await _moveNextSignal.Wait().ConfigureAwait(false);
                    await producerFunc(producer);
                }
                finally
                {
                    enumerator.EndOfStream();
                }
            };
        }
    }
}