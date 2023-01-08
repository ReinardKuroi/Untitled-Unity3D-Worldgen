using System;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;

namespace TerrainGenerator {
    public class ThreadDispatcher {

        private readonly int MAX_THREADS = 8;
        readonly Stack<Thread> threadsInQueue = new();
        readonly Queue<Thread> threadsRunning = new();
        readonly Stack<Thread> threadsCompleted = new();
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
                    Debug.Log($"Thread started: {worker.Name}");
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
                Debug.Log($"Thread finished: {worker.Name} {worker.ThreadState}");
                RunCallback(worker.ManagedThreadId);
            }
        }

        void RunCallback(int workerId) {
            Debug.Log($"Thread callback {workerId}: {threadCallbacks[workerId].Method}");
            threadCallbacks[workerId]();
            DropCallback(workerId);
        }

        void DropCallback(int workerId) {
            threadCallbacks.Remove(workerId);
            Debug.Log($"Thread callback dropped: {workerId}");
        }

        public void Flush() {
            threadsInQueue.Clear();
            while (threadsRunning.TryDequeue(out Thread worker)) {
                worker.Abort();
                Debug.Log($"Thread flushed: {worker.Name} {worker.ThreadState}");
                DropCallback(worker.ManagedThreadId);
            }
        }
    }
}