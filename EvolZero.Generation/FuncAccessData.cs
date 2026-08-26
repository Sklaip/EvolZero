using EvolZero.Core;
using EvolZero.Generation.Accessors;
namespace EvolZero.Generation
{
	public class FuncAccessData
	{
		public readonly IFuncRefData Refs = new FuncRefData();
		public IValueAccessor[] Arguments { get; set; }

		public FuncAccessData(IValueAccessor[] arguments, IFuncRefData refs)
		{
			Refs = refs;
			Arguments = arguments;
		}
	}
}
