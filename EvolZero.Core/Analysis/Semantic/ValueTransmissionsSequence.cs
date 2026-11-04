using System.Diagnostics.CodeAnalysis;

namespace EvolZero.Core.Analysis.Semantic
{
    internal class ValueTransmissionsSequence
    {
        public record TransmissionMeta
        {
            public VarMeta? Value { get; }
            public Lifetime? Lifetime { get; }

            public TransmissionMeta(VarMeta? value)
            {
                Value = value;
            }

            public TransmissionMeta(Lifetime? lifetime)
            {
                Lifetime = lifetime;
            }

            public TransmissionMeta() { }

            public bool IsNull => Value == null && Lifetime == null;

            public Lifetime? GetRealLifetime() => Value?.Lifetime ?? Lifetime;
        }

        private readonly Stack<TransmissionMeta> _transmissions = new();

        public ValueTransmissionsSequence()
        {
            AddNull();
        }

        public void AddValue(VarMeta? value)
        {
            _transmissions.Push(new TransmissionMeta(value));
        }

        public void AddLifetime(Lifetime lifetime)
        {
            _transmissions.Push(new TransmissionMeta(lifetime));
        }

        public void AddNull()
        {
            _transmissions.Push(new TransmissionMeta());
        }

        public TransmissionMeta GetCurrent()
        {
            return _transmissions.Peek();
        }

        public TransmissionMeta RemoveCurrent()
        {
            return _transmissions.Pop();
        }

        //public bool GetCurrentLifetime([NotNullWhen(true)] out Lifetime? lifetime)
        //{ 
        //    var current = GetCurrent();
        //    lifetime = current.Lifetime ?? current.Value?.Lifetime;

        //    return lifetime != null;
        //}

        //public bool GetCurrentValue([NotNullWhen(true)] out VarMeta? value)
        //{
        //    var data = _transmissions.FirstOrDefault(x => x.IsNull || x.Value != null);
        //    value = data?.Value;
        //    return value != null;
        //}
    }
}
