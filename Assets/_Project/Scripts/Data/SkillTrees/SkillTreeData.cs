using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace InElementum.Data.SkillTrees
{
    /// <summary>
    /// One character's skill tree: nodes on a grid, linked by prerequisites. The only source of Surges and of every
    /// ability that does not start unlocked. Edited in Tools/In Elementum/Skill Tree Editor.
    /// </summary>
    [CreateAssetMenu(fileName = "Skill Tree", menuName = "In Elementum/Characters/Skill Tree", order = 2)]
    public class SkillTreeData : ScriptableObject
    {
        [SerializeField] private List<SkillNodeData> nodes = new List<SkillNodeData>();

        public List<SkillNodeData> Nodes { get => nodes; internal set => nodes = value; }

        public SkillNodeData Find(string id) =>
            string.IsNullOrEmpty(id) ? null : nodes.FirstOrDefault(node => node != null && node.Id == id);

        public SkillNodeData FindAt(Vector2Int cell) => nodes.FirstOrDefault(node => node != null && node.Position == cell);

        /// <summary>Nodes whose prerequisites include <paramref name="node"/>.</summary>
        public IEnumerable<SkillNodeData> Dependents(SkillNodeData node) =>
            node == null ? Enumerable.Empty<SkillNodeData>() : nodes.Where(other => other != null && other.Prerequisites.Contains(node.Id));

        /// <summary>Problems a designer should fix: blank or repeated ids, and links to nodes that do not exist.</summary>
        public List<string> Validate()
        {
            var problems = new List<string>();
            var seen = new HashSet<string>();
            var cells = new HashSet<Vector2Int>();

            foreach (var node in nodes.Where(node => node != null))
            {
                if (string.IsNullOrEmpty(node.Id)) problems.Add($"'{node.DisplayName}' has no id.");
                else if (!seen.Add(node.Id)) problems.Add($"Id '{node.Id}' is used twice.");
                if (!cells.Add(node.Position)) problems.Add($"Two nodes share cell {node.Position}.");

                foreach (var prerequisite in node.Prerequisites.Where(id => Find(id) == null))
                    problems.Add($"'{node.DisplayName}' links to missing node '{prerequisite}'.");

                if (node.Kind == SkillNodeKind.Ability && node.Ability == null) problems.Add($"'{node.DisplayName}' has no ability.");
                if (node.Kind == SkillNodeKind.Surge && node.Surge == null) problems.Add($"'{node.DisplayName}' has no Surge.");
                if (node.Kind == SkillNodeKind.Passive && node.Passive == null) problems.Add($"'{node.DisplayName}' has no passive.");
            }

            return problems;
        }
    }
}
