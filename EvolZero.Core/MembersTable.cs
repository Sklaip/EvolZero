using EvolZero.Core.MemebersModels;

namespace EvolZero.Core
{
    public class MembersTable
    {
        public readonly Dictionary<string, FuncDesc[]> Functions;
        public readonly Dictionary<string, TypeDesc> Types;

        public MembersTable(Dictionary<string, FuncDesc[]> functions, Dictionary<string, TypeDesc> types)
        {
            Functions = functions;
            Types = types;
        }

        public MembersTable() : this([], []) { }

        public void Merge(MembersTable other)
        {
            foreach (var func in other.Functions)
            {
                Functions[func.Key] = func.Value;
            }

            foreach (var type in other.Types)
            {
                Types[type.Key] = type.Value;
            }
        }

        public List<(TypeDesc Type, string FiledName)> CheckCyclicalDependence()
        {
            var res = new List<(TypeDesc Type, string FiledName)>();
            foreach (var currentType in Types.Values)
            {
                string? fieldName = HasCyclicalDependence(currentType, currentType);
                if (fieldName != null)
                {
                    res.Add((currentType, fieldName));
                }
            }

            return res;
        }

        private string? HasCyclicalDependence(TypeDesc currentType, TypeDesc foundType)
        {
            foreach (var field in currentType.Variables.Values)
            {
                if (field.Declaring.IsRef) continue;
                if (foundType == field.Declaring.Type) return field.Name;

                string? fieldName = HasCyclicalDependence(field.Declaring.Type, foundType);
                if (fieldName != null) return fieldName;
            }

            return null;
        }
    }
}
