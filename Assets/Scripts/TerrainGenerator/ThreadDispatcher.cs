using System;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;

namespace TerrainGenerator {
    public class ThreadDispatcher {

        private const int MAX_THREADS = 12;
        readonly Queue<Thread> threadsInQueue = new();
        readonly Queue<Thread> threadsRunning = new();
        readonly Queue<Thread> threadsCompleted = new();
        readonly Dictionary<int, Action> threadCallbacks = new();

        public static ThreadDispatcher Instance { get {
                if (instance == null) {
                    instance = new ThreadDispatcher();
                    Debug.Log($"Initialized new ThreadDispatcher {instance}: numThreads {MAX_THREADS}");
                }
                return instance;
            } }
        private static ThreadDispatcher instance;

        public void UpdateThreads() {
            Run();
            CheckStatus();
            Join();
        }

        public Thread EnqueueThread(Action Invoke, Action Callback = null, string workerName = null) {
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
            Debug.Log($"Enqueued new thread {worker.ManagedThreadId}: Invoke {Invoke.Method} Callback {Callback.Method}");
            return worker;
        }

        void Run() {
            int newThreadCount = 0;
            while (threadsRunning.Count < MAX_THREADS) {
                if (threadsInQueue.TryDequeue(out Thread worker)) {
                    worker.Start();
                    threadsRunning.Enqueue(worker);
                    ++newThreadCount;
                    Debug.Log($"Started thread {worker.ManagedThreadId}");
                } else {
                    break;
                }
            }
            if (newThreadCount > 0) {
                Debug.Log($"Added Threads: {newThreadCount}");
                Debug.Log($"Threads Running: {threadsRunning.Count}");
            }
        }

        void CheckStatus() {
            for (int i = 0; i < threadsRunning.Count; ++i) {
                Thread worker = threadsRunning.Dequeue();
                if (worker.IsAlive) {
                    threadsRunning.Enqueue(worker);
                    Debug.Log($"Thread still running: {worker.ManagedThreadId}");
                } else {
                    threadsCompleted.Enqueue(worker);
                    Debug.Log($"Thread marked as complete: {worker.ManagedThreadId}");
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
            threadsInQueue.Clear();
            while (threadsRunning.TryDequeue(out Thread worker)) {
                worker.Abort();
                DropCallback(worker.ManagedThreadId);
            }
        }
    }
}