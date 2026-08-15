using System;

namespace Ceres.RuntimeConsole
{
	[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
	public class ConsoleMethodAttribute : ConsoleAttribute
	{
		public string Command { get; }

		public string Description { get; }

		public string[] ParameterNames { get; }

		public override int Order => 1;

		public ConsoleMethodAttribute(string command, string description, params string[] parameterNames)
		{
			Command = command;
			Description = description;
			ParameterNames = parameterNames;
		}

		public override void Load()
		{
			DebugLogConsole.AddCommand(Command, Description, Method, null, ParameterNames);
		}
	}
}