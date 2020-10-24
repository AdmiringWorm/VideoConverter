namespace VideoConverter.DependencyInjection
{
    using System;
    using System.Linq;
    using DryIoc;
    using Spectre.Cli;

    public class TypeResolver : ITypeResolver
    {
        private readonly IContainer container;

        public TypeResolver(IContainer container)
        {
            this.container = container;
        }

        public object? Resolve(Type? type)
        {
            var result = this.container.Resolve(type);

            if (type.GetImplementedTypes().Any(t => t.IsGenericType && (t.GetGenericTypeDefinition() == typeof(AsyncCommand<>) || t.GetGenericTypeDefinition() == typeof(Command<>))))

            {
                this.container.InjectPropertiesAndFields(result);
            }

            return result;
        }
    }
}
