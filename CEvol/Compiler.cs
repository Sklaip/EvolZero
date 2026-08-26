using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using EvolZero.Core;
using EvolZero.Core.Analysis.Semantic;
using EvolZero.Core.LogicModels.Statements;
using EvolZero.Core.MemebersModels;
using EvolZero.Generation;
using EvolZero.Parsing;
using LLVMSharp.Interop;
using System.Runtime.InteropServices;
using System.Text;

namespace EvolZero
{
	internal class Compiler
	{
		private LLVMTargetMachineRef _targetMachine;

		private class FileUnit
		{
			public string FilePath { get; set; } = string.Empty;
			public string Content { get; set; } = string.Empty;
			public IParseTree Tree { get; set; } = null!;
			public MembersTableBuilder TableBuilder { get; set; } = null!;
			public string Namespace { get; set; } = null!;
		}

		public void Execute(List<string> inputFiles, string outputFile)
		{
			LLVM.InitializeNativeTarget();
			LLVM.InitializeNativeAsmPrinter();
			LLVM.InitializeNativeAsmParser();

			LLVM.InitializeAllTargetInfos();
			LLVM.InitializeAllTargets();
			LLVM.InitializeAllTargetMCs();
			LLVM.InitializeAllAsmPrinters();

			var sourcesMap = new Dictionary<string, string>();
			var units = new List<FileUnit>();

			foreach (var filePath in inputFiles)
			{
				var fileContent = File.ReadAllText(filePath);
				sourcesMap[filePath] = fileContent;

				ICharStream stream = CharStreams.fromString(fileContent);
				var lexer = new CEvolLexer(stream);
				ITokenStream tokenStream = new CommonTokenStream(lexer);
				var parser = new CEvolParser(tokenStream);

				IParseTree tree = parser.program();

				units.Add(new FileUnit
				{
					FilePath = filePath,
					Content = fileContent,
					Tree = tree
				});
			}

			string moduleName = Path.GetFileNameWithoutExtension(outputFile);
			if (string.IsNullOrWhiteSpace(moduleName))
			{
				moduleName = "main_module";
			}

			var codeGenerator = BuildCodeGenerator(moduleName);
			var globalMembersTable = BuildBaseMembersTable(codeGenerator);
			var globalTypesList = new Dictionary<string, TypeDesc>(globalMembersTable.Types);

			foreach (var unit in units)
			{
				var visitor = new MembersVisitor();
				visitor.Visit(unit.Tree);

				var membersTableBuilder = new MembersTableBuilder(visitor.CurrentNameSpace,
					visitor.Usings, visitor.Classes, visitor.SingleFunctions, codeGenerator);

				membersTableBuilder.PrepareTable(globalTypesList);
				unit.TableBuilder = membersTableBuilder;
				unit.Namespace = visitor.CurrentNameSpace;
			}

			foreach (var unit in units)
			{
				var currentTable = unit.TableBuilder.Build();
				globalMembersTable.Merge(currentTable);
			}

			var finder = new MembersFinder(globalMembersTable);
			foreach (var unit in units)
			{
				if (!string.IsNullOrEmpty(unit.Namespace))
				{
					finder.AddNamespace(unit.Namespace);
				}
			}

			var errorsBag = new ErrorsBag(sourcesMap);
			var program = new ProgramStatement(default);

			foreach (var unit in units)
			{
				var logicVisitor = new LogicVisitor(finder, errorsBag, unit.FilePath);
				logicVisitor.Visit(unit.Tree);
				var statement = logicVisitor.ResultStatement;

				program.AddStatement(statement);
			}

			if (errorsBag.HasErrors)
			{
				Console.WriteLine(errorsBag.BuildErrorsMessage());
				return;
			}

			var referencesAnalazer = new ReferencesAnalyzer(errorsBag);
			referencesAnalazer.Visit(program);

			var accessAnalazer = new AccessAnalyzer(errorsBag);
			accessAnalazer.Visit(program);

			if (!errorsBag.HasErrors)
			{
				var emitter = new Emitter(codeGenerator);
				emitter.Build(program);

				Compile(emitter.CodeGenerator, outputFile);
			}
			else
			{
				Console.WriteLine(errorsBag.BuildErrorsMessage());
			}
		}

		private MembersTable BuildBaseMembersTable(CodeGenerator codeGenerator)
		{
			var types = new Dictionary<string, TypeDesc>();
			types["void"] = new TypeDesc("void", codeGenerator.GetType(BaseTypes.Void));
			types["bool"] = new TypeDesc("bool", codeGenerator.GetType(BaseTypes.Bool));
			types["byte"] = new IntegerTypeDesc("byte", codeGenerator.GetType(BaseTypes.Byte));
			types["short"] = new IntegerTypeDesc("short", codeGenerator.GetType(BaseTypes.Short));
			types["int"] = new IntegerTypeDesc("int", codeGenerator.GetType(BaseTypes.Int));
			types["long"] = new IntegerTypeDesc("long", codeGenerator.GetType(BaseTypes.Long));
			types["sbyte"] = new IntegerTypeDesc("sbyte", codeGenerator.GetType(BaseTypes.Byte));
			types["ushort"] = new IntegerTypeDesc("ushort", codeGenerator.GetType(BaseTypes.Short));
			types["uint"] = new IntegerTypeDesc("uint", codeGenerator.GetType(BaseTypes.Int));
			types["ulong"] = new IntegerTypeDesc("long", codeGenerator.GetType(BaseTypes.Long));
			types["float"] = new FloatTypeDesc("float", codeGenerator.GetType(BaseTypes.Float));
			types["double"] = new FloatTypeDesc("double", codeGenerator.GetType(BaseTypes.Double));

			types["short"].CanExpandedTo.Add(types["int"]);
			types["sbyte"].CanExpandedTo.Add(types["short"]);

			types["ushort"].CanExpandedTo.AddRange(types["uint"], types["int"]);
			types["byte"].CanExpandedTo.AddRange(types["short"], types["ushort"]);

			types["int"].CanExpandedTo.AddRange(types["long"], types["float"]);
			types["uint"].CanExpandedTo.AddRange(types["long"], types["ulong"], types["float"]);

			types["long"].CanExpandedTo.Add(types["double"]);
			types["ulong"].CanExpandedTo.AddRange(types["double"]);
			types["float"].CanExpandedTo.Add(types["double"]);

			return new MembersTable([], types);
		}

		private unsafe CodeGenerator BuildCodeGenerator(string moduleName)
		{
			string triple = LLVMTargetRef.DefaultTriple;
			var target = LLVMTargetRef.GetTargetFromTriple(triple);
			_targetMachine = target.CreateTargetMachine(
				triple,
				cpu: "generic",
				features: "",
				LLVMCodeGenOptLevel.LLVMCodeGenLevelDefault,
				LLVMRelocMode.LLVMRelocDefault,
				LLVMCodeModel.LLVMCodeModelDefault
			);

			LLVMTargetDataRef dataLayoutRef = _targetMachine.CreateTargetDataLayout();
			string dataLayoutString = new string(LLVM.CopyStringRepOfTargetData(dataLayoutRef));

			var context = LLVMContextRef.Create();
			var module = context.CreateModuleWithName(moduleName);

			module.Target = triple;
			module.DataLayout = dataLayoutString;

			return new CodeGenerator(context, module);
		}

		private void Compile(CodeGenerator codeGenerator, string targetExePath)
		{
			var module = codeGenerator.GetModule();

			Console.WriteLine("================ ИСХОДНЫЙ IR ================");
			module.Dump();

			codeGenerator.VerifyModule();

			Console.WriteLine("\n================ ОПТИМИЗИРОВАННЫЙ IR ================");
			Optimize(module);

			string objFileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "output.obj" : "output.o";

			if (_targetMachine.TryEmitToFile(module, objFileName, LLVMCodeGenFileType.LLVMObjectFile, out string errorMessage))
			{
				Console.WriteLine($"Объектный файл успешно создан: {objFileName}");
			}
			else
			{
				Console.WriteLine($"Ошибка генерации: {errorMessage}");
				return;
			}

			LinkExecutable(objFileName, targetExePath);
		}

		private unsafe void Optimize(LLVMModuleRef module)
		{
			var triple = LLVMTargetRef.DefaultTriple;
			var target = LLVMTargetRef.GetTargetFromTriple(triple);

			var targetMachine = target.CreateTargetMachine(
				triple,
				"generic",
				"",
				LLVMCodeGenOptLevel.LLVMCodeGenLevelDefault,
				LLVMRelocMode.LLVMRelocDefault,
				LLVMCodeModel.LLVMCodeModelDefault
			);

			LLVMOpaquePassBuilderOptions* passOptions = LLVM.CreatePassBuilderOptions();
			byte[] passesBytes = Encoding.UTF8.GetBytes("default<O2>\0");

			LLVMOpaqueError* error = null;

			fixed (byte* pPasses = passesBytes)
			{
				error = LLVM.RunPasses(
					(LLVMOpaqueModule*)module.Handle,
					(sbyte*)pPasses,
					(LLVMOpaqueTargetMachine*)targetMachine.Handle,
					passOptions
				);
			}

			if (error != null)
			{
				sbyte* errMsg = LLVM.GetErrorMessage(error);
				string message = Marshal.PtrToStringUTF8((IntPtr)errMsg);
				Console.WriteLine($"Ошибка оптимизации: {message}");
				LLVM.DisposeErrorMessage(errMsg);
			}
			else
			{
				string optimizedIR = module.PrintToString();
				Console.WriteLine(optimizedIR);
			}

			LLVM.DisposePassBuilderOptions(passOptions);
		}


		private void LinkExecutable(string objFile, string exeFile)
		{
			using var process = new System.Diagnostics.Process();

			process.StartInfo.FileName = "clang";
			process.StartInfo.Arguments = $"{objFile} -o {exeFile} -llegacy_stdio_definitions";

			process.StartInfo.UseShellExecute = false;
			process.StartInfo.RedirectStandardError = true;

			try
			{
				process.Start();
				process.WaitForExit();
				if (process.ExitCode != 0)
				{
					string errors = process.StandardError.ReadToEnd();
					Console.WriteLine($"Ошибка линковщика:\n{errors}");
				}
				else
				{
					Console.WriteLine($"Исполняемый файл {exeFile} успешно собран!");
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Ошибка запуска линковщика: {ex.Message}");
			}
		}
	}
}