using System;

namespace Amity
{
    [AttributeUsage(AttributeTargets.Method)]
    public class AmityPatch : Attribute
    {
        public Type Type { get; }
        public string MethodName { get; }
        public Mode CodeMode { get; }
        public int CustomPos { get; }
        public Type[] Parameters { get; }
        
        public AmityPatch(Type type, string methodName, Mode mode, int customPos = 0, Type[] parameters = null)
        {
            Type = type;
            MethodName = methodName;
            CodeMode = mode;
            CustomPos = customPos;
            Parameters = parameters ?? new Type[0];
        }
        
        public enum Mode
        {
            Prefix,
            Postfix,
            Replace,
            Custom
        }
    }
}