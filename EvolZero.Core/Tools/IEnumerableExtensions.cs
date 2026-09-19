using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.Core.Tools
{
	public static class IEnumerableExtensions
	{
		public static void ForEach<T>(this IEnumerable<T> enumerable, Action<T> handler)
		{
			if (enumerable == null) return;

			foreach (var item in enumerable)
			{
				handler(item);
			}
		}
	}
}
