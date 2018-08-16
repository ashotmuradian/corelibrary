using System.Collections.Generic;

namespace LeanCode.ContractsGenerator.Statements
{
    class ClientStatement : IStatement
    {
        public List<IStatement> Children { get; set; } = new List<IStatement>();
        public string Name { get; set; } = string.Empty;
    }
}

