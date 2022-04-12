using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;
using System;

public class ThreadedDataRequester
{
	private static readonly Lazy<ThreadedDataRequester> _instance =
		 new Lazy<ThreadedDataRequester>(() => new ThreadedDataRequester());
	Queue<ThreadInfo> dataQueue = new Queue<ThreadInfo>();

	public static ThreadedDataRequester Instance { get { return _instance.Value; } }

	public static void RequestData(Func<object> generateData, Action<object> callback)
	{
		ThreadStart threadStart = delegate {
			_instance.Value.DataThread(generateData, callback);
		};

		new Thread(threadStart).Start();
	}

	void DataThread(Func<object> generateData, Action<object> callback)
	{
		object data = generateData();
		lock (dataQueue)
		{
			dataQueue.Enqueue(new ThreadInfo(callback, data));
		}
	}


	void Update()
	{
		if (dataQueue.Count > 0)
		{
			for (int i = 0; i < dataQueue.Count; i++)
			{
				ThreadInfo threadInfo = dataQueue.Dequeue();
				threadInfo.callback(threadInfo.parameter);
			}
		}
	}

	struct ThreadInfo
	{
		public readonly Action<object> callback;
		public readonly object parameter;

		public ThreadInfo(Action<object> callback, object parameter)
		{
			this.callback = callback;
			this.parameter = parameter;
		}

	}
}
