using System;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;

namespace TerrainGenerator {
    public class ThreadDispatcher {

        private const int MAX_THREADS = 8;
        readonly Queue<Thread> threadsInQueue = new();
        readonly Queue<Thread> threadsRunning = new();
        readonly Queue<Thread> threadsCompleted = new();
        readonly List<int> threadsToKill = new();
        readonly Dictionary<int, Action> threadCallbacks = new();

        public static ThreadDispatcher Instance { get {
                if (instance == null) {
                    instance = new ThreadDispatcher();
                }
                return instance;
            } }
        private static ThreadDispatcher instance;

        public void UpdateThreads() {
            Run();
            CheckStatus();
            Join();
        }

        public int EnqueueThread(Action Invoke, Action Callback = null, string workerName = null) {
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
            threadsInQueue.Enqueue(worker);

            if (Callback != null) {
                threadCallbacks.Add(worker.ManagedThreadId, Callback);
            }
            return worker.ManagedThreadId;
        }

        void Run() {
            while (threadsRunning.Count < MAX_THREADS) {
                if (threadsInQueue.TryDequeue(out Thread worker)) {
                    if (threadsToKill.Contains(worker.ManagedThreadId)) {
                        threadsToKill.Remove(worker.ManagedThreadId);
                        continue;
                    }
                    worker.Start();
                    threadsRunning.Enqueue(worker);
                } else {
                    break;
                }
            }
        }

        void CheckStatus() {
            for (int i = 0; i < threadsRunning.Count; ++i) {
                Thread worker = threadsRunning.Dequeue();
                if (threadsToKill.Contains(worker.ManagedThreadId)) {
                    worker.Abort();
                    DropCallback(worker.ManagedThreadId);
                    threadsToKill.Remove(worker.ManagedThreadId);
                } else if (worker.IsAlive) {
                    threadsRunning.Enqueue(worker);
                } else {
                    threadsCompleted.Enqueue(worker);
                }
            }
        }

        void Join() {
            while (threadsCompleted.TryDequeue(out Thread worker)) {
                worker.Join();
                RunCallback(worker.ManagedThreadId);
            }
        }

        void RunCallback(int workerId) {
            threadCallbacks[workerId]();
            DropCallback(workerId);
        }

        void DropCallback(int workerId) {
            threadCallbacks.Remove(workerId);
        }

        public void Flush() {
            while (threadsRunning.TryDequeue(out Thread worker)) {
                TryKill(worker.ManagedThreadId);
            }
            while (threadsInQueue.TryDequeue(out Thread worker)) {
                threadsToKill.Remove(worker.ManagedThreadId);
            }
        }

        internal void TryKill(int workerId) {
            if (workerId != 0) {
                threadsToKill.Add(workerId);
            }
        }
    }
}