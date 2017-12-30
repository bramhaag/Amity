using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Amity
{
    public static class AmityInstance
    {
        /// <summary>
        /// Patch assembly
        /// </summary>
        /// <param name="assemblyPath">Path to assembly</param>
        /// <param name="finalPath">Destinatoin of patched assembly</param>
        /// <param name="patchClasses">Classes to search for patches</param>
        public static void Patch(string assemblyPath, string finalPath, params Type[] patchClasses)
        {
            Array.ForEach(patchClasses, patchClass => Patch(patchClass, assemblyPath, finalPath));
        }
        
        /// <summary>
        /// Patch assembly
        /// </summary>
        /// <param name="patchClass">Class to search for patches</param>
        /// <param name="assemblyPath">Path to assembly</param>
        /// <param name="finalPath">Destinatoin of patched assembly</param>
        public static void Patch(Type patchClass, string assemblyPath, string finalPath)
        {
            var methods = patchClass.GetMethods()
                .Where(m => m.GetCustomAttribute(typeof(AmityPatch)) != null)
                .ToDictionary(m => m, m => m.GetCustomAttribute(typeof(AmityPatch)));

            foreach (var entry in methods)
            {
                var method = entry.Key;
                var attribute = (AmityPatch) entry.Value;
                
                var patchModule = ModuleDefMD.Load(patchClass.Module);
                var patchInstructions = FindMethod(patchModule, patchClass, method.Name).Body.Instructions;

                var assemblyModule = ModuleDefMD.Load(assemblyPath);
                var assemblyMethod = FindMethod(assemblyModule, attribute.Type, attribute.MethodName,  attribute.Parameters);
                var assemblyInstructions = assemblyMethod.Body.Instructions;
                
                List<Instruction> patchedInstructions;
                
                switch (attribute.CodeMode)
                {
                    case AmityPatch.Mode.Prefix:
                        patchedInstructions = MergeInstructions(patchInstructions, assemblyInstructions);
                        break;
                    case AmityPatch.Mode.Postfix:
                        patchedInstructions = MergeInstructions(assemblyInstructions, patchInstructions);
                        break;
                    case AmityPatch.Mode.Replace:
                        patchedInstructions = (List<Instruction>) patchInstructions;
                        break;
                    case AmityPatch.Mode.Custom:
                        patchedInstructions =
                            MergeInstructions(assemblyInstructions, patchInstructions, attribute.CustomPos);
                        break;
                    default:
                        patchedInstructions = new List<Instruction>();
                        break;
                }

                assemblyMethod.Body.Instructions.Clear();
                patchedInstructions.ForEach(instruction => assemblyMethod.Body.Instructions.Add(instruction));

                assemblyModule.Write(finalPath);
            }
        }

        /// <summary>
        /// Find method with name <paramref name="methodName"/> in <paramref name="module"/>
        /// </summary>
        /// <param name="module">Module to search in</param>
        /// <param name="type">Class to search for</param>
        /// <param name="methodName">Name of method</param>
        /// <param name="parameters">Parameters of method</param>
        /// <returns>Found method</returns>
        private static MethodDef FindMethod(ModuleDef module, Type type, string methodName,
            IReadOnlyList<Type> parameters = null)
        {
            return module.GetTypes()
                //TODO make this line less ugly
                .First(t => (t.Namespace ?? "") == (type.Namespace ?? "") && t.Name == type.Name)
                .FindMethods(methodName)
                .First(m => parameters == null || CompareParameters(m.Parameters, parameters));

        }

        /// <summary>
        /// Compare types of <paramref name="parameterList"/> to <paramref name="parameterTypes"/>
        /// </summary>
        /// <param name="parameterList">List of parameters</param>
        /// <param name="parameterTypes">Types of parameters</param>
        /// <returns>Result of comparision</returns>
        private static bool CompareParameters(ParameterList parameterList, IReadOnlyList<Type> parameterTypes)
        {
            if (parameterList.Count != parameterTypes.Count) return false;
            if (parameterList.Count == 0 && parameterTypes.Count == 0) return true;
            
            return !parameterList.Where((t, i) => t.Type.FullName != parameterTypes[i].FullName).Any();
        }

        /// <summary>
        /// Remove <code>nop</code> instructions
        /// </summary>
        /// <param name="instructions">List of instructions to filter</param>
        private static void RemoveNop(ref IList<Instruction> instructions)
        {
            instructions = instructions
                .Where(instruction => instruction.OpCode.Name != "nop")
                .ToList();
        }
        
        /// <summary>
        /// Remove <code>ret</code> instructions.
        /// Also removes <code>nop</code> instructions unless <paramref name="keepNop"/> is set to true
        /// </summary>
        /// <param name="instructions">List of instructions to filter</param>
        /// <param name="keepNop">Keep <code>nop</code> instructions</param>
        private static void RemoveReturn(ref IList<Instruction> instructions, bool keepNop = false)
        {
            instructions = instructions
                .Where(instruction => instruction.OpCode.Name != "ret")
                .Where(instruction => keepNop || instruction.OpCode.Name != "nop")
                .ToList();
        }

        /// <summary>
        /// Concat <paramref name="newInstructions"/> to <paramref name="baseInstructions"/>
        /// </summary>
        /// <param name="baseInstructions">First list of instructions</param>
        /// <param name="newInstructions">Second list of instructions</param>
        /// <returns><paramref name="newInstructions"/> concatenated to <paramref name="baseInstructions"/></returns>
        private static List<Instruction> MergeInstructions(IList<Instruction> baseInstructions,
            // ReSharper disable once ParameterTypeCanBeEnumerable.Local
            IList<Instruction> newInstructions)
        {
            RemoveReturn(ref baseInstructions);
            return baseInstructions.Concat(newInstructions).ToList();
        }

        /// <summary>
        /// Insert <paramref name="newInstructions"/> at <paramref name="index"/> in <paramref name="baseInstructions"/>
        /// </summary>
        /// <param name="baseInstructions">Original list of instructions</param>
        /// <param name="newInstructions">List of instructions to insert into <paramref name="baseInstructions"/></param>
        /// <param name="index">Position to insert <paramref name="newInstructions"/> at</param>
        /// <returns>
        /// List with <paramref name="newInstructions"/> inserted at <paramref name="index"/> in <paramref name="baseInstructions"/>
        /// </returns>
        private static List<Instruction> MergeInstructions(IList<Instruction> baseInstructions,
            IList<Instruction> newInstructions, int index)
        {
            if (index > baseInstructions.Count - 1)
            {
                RemoveNop(ref newInstructions);
                return MergeInstructions(baseInstructions, newInstructions);
            }
                        
            RemoveReturn(ref newInstructions);
            var instructions = new List<Instruction>();
            instructions.AddRange(baseInstructions);
            instructions.InsertRange(index, newInstructions);

            return instructions;
        }
    }
}