using Dalamud.Bindings.ImGui;
using System.Numerics;

namespace Sightseeingway.UI.Components
{
    /// <summary>
    /// Three-state group toggle: unchecked / checked / indeterminate.
    /// Indeterminate is rendered with a half-tone fill to communicate
    /// "some children on, some off" at a glance.
    ///
    /// Click semantics (standard tree-checkbox convention):
    /// - Unchecked → all children should be turned ON.
    /// - Checked or Indeterminate → all children should be turned OFF.
    /// </summary>
    public enum CheckState
    {
        Unchecked,
        Checked,
        Indeterminate,
    }

    public static class TriStateCheckbox
    {
        /// <summary>
        /// Renders a tri-state checkbox. Returns true if the user clicked it.
        /// The intended action when clicked is encoded in <paramref name="state"/>:
        /// click on an Unchecked state means "turn all children ON";
        /// click on Checked or Indeterminate means "turn all children OFF".
        /// </summary>
        public static bool Draw(string id, CheckState state)
        {
            var clicked = false;

            // Render the checkbox manually so the indeterminate state has a clear
            // visual identity. We mirror ImGui's stock checkbox layout: a square
            // box at line height followed by an inline label.
            var drawList = ImGui.GetWindowDrawList();
            var pos = ImGui.GetCursorScreenPos();
            var size = ImGui.GetFrameHeight();
            var boxSize = new Vector2(size, size);

            ImGui.PushID(id);
            if (ImGui.InvisibleButton("##box", boxSize))
                clicked = true;
            ImGui.PopID();

            var hovered = ImGui.IsItemHovered();
            var bgColor = ImGui.GetColorU32(hovered ? ImGuiCol.FrameBgHovered : ImGuiCol.FrameBg);
            var checkColor = ImGui.GetColorU32(ImGuiCol.CheckMark);

            drawList.AddRectFilled(pos, pos + boxSize, bgColor, 2f);
            drawList.AddRect(pos, pos + boxSize, ImGui.GetColorU32(ImGuiCol.Border), 2f);

            switch (state)
            {
                case CheckState.Checked:
                {
                    var pad = size * 0.2f;
                    var p0 = pos + new Vector2(pad, size * 0.55f);
                    var p1 = pos + new Vector2(size * 0.42f, size - pad);
                    var p2 = pos + new Vector2(size - pad, pad);
                    drawList.AddLine(p0, p1, checkColor, 2f);
                    drawList.AddLine(p1, p2, checkColor, 2f);
                    break;
                }
                case CheckState.Indeterminate:
                {
                    var pad = size * 0.25f;
                    drawList.AddRectFilled(
                        pos + new Vector2(pad, pad),
                        pos + new Vector2(size - pad, size - pad),
                        checkColor, 1f);
                    break;
                }
            }

            return clicked;
        }
    }
}
