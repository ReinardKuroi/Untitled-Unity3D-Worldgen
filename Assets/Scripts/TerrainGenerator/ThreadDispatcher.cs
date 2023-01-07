using System;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;

namespace TerrainGenerator {
    class ThreadDispatcher {

        private const int MAX_THREADS = 8;
        readonly Stack<Thread> threadsInQueue = new();
        readonly Queue<Thread> threadsRunning = new();
        readonly Stack<Thread> threadsCompleted = new();
        readonly Dictionary<int, Action> threadCallbacks = new();

        public void UpdateThreads() {
            Run();
            CheckStatus();
            Join();
        }

        public Thread EnqueueThread(Action Invoke, Action callback = null, string workerName = null) {
            Thread worker = new(() => {
                try {
                    Invoke();
                }
                catch (Exception exc) {
                    Debug.LogError(exc);
                }
            });

            if (workerName == null) {
                workerName = threadsInQueue.Count.ToString();
            }
            worker.Name = workerName;
            threadsInQueue.Push(worker);

            if (callback != null) {
                threadCallbacks.Add(worker.ManagedThreadId, callback);
            }

            return worker;
        }

        void Run() {
            while (threadsRunning.Count < MAX_THREADS) {
                if (threadsInQueue.TryPop(out Thread worker)) {
                    worker.Start();
                    Debug.Log($"Thread {worker.Name} started");
                    threadsRunning.Enqueue(worker);
                } else {
                    break;
                }
            }
        }

        void CheckStatus() {
            for (int i = 0; i < threadsRunning.Count; ++i) {
                Thread worker = threadsRunning.Dequeue();
                if (worker.IsAlive) {
                    threadsRunning.Enqueue(worker);
                } else {
                    threadsCompleted.Push(worker);
                }
            }
        }

        void Join() {
            while (threadsCompleted.TryPop(out Thread worker)) {
                worker.Join();
                threadCallbacks[worker.ManagedThreadId]();
                Debug.Log($"Thread {worker.Name} finished");
            }
        }
    }
}