using System;
using System.Collections.Generic;
using System.Text;

namespace EvolZero.CLI
{
	internal class CommandLineManager
	{
		private readonly string[] _args;
		private readonly IEnumerable<ICommandExecutor> _allCommandExecutors;

		public CommandLineManager(string[] args, IEnumerable<ICommandExecutor> allCommandExecutors)
		{
			_args = args;
			_allCommandExecutors = allCommandExecutors;
		}

		public string DefineExecutor()
		{
			if (_args.Length < 1)
			{
				return "argumentsError";
			}

			ICommandExecutor? selectedExecutor = null;

			string firstArgument = _args[0];
			selectedExecutor = _allCommandExecutors.FirstOrDefault(x => x.Name == firstArgument);

			if(selectedExecutor == null)
			{
				selectedExecutor = _allCommandExecutors.FirstOrDefault(x => x.IsDefault);
				if (selectedExecutor == null) return "noExecutor";

				return selectedExecutor.Execute(_args);
			}

			return selectedExecutor.Execute(_args.Skip(1));
		}
	}
}
