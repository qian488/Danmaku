using UnityEngine;
using UnityEditor;
using TMPro;

namespace DemoFrameWork.Editor
{
    /// <summary>
    /// 解决 TMP Font Asset 的 Font Material 在 Inspector 中置灰无法拖拽的问题：
    /// 通过代码为选中的字体资源绑定同目录下名称匹配的材质。
    /// </summary>
    public static class TMPFontMaterialFixer
    {
        private const string MenuName = "Tools/DemoFrameWork/为选中的 TMP 字体指定材质";

        [MenuItem(MenuName, true)]
        private static bool ValidateAssignMaterial()
        {
            if (Selection.activeObject == null) return false;
            return Selection.activeObject is TMP_FontAsset;
        }

        [MenuItem(MenuName)]
        private static void AssignMaterialToSelectedFont()
        {
            var font = Selection.activeObject as TMP_FontAsset;
            if (font == null)
            {
                Debug.LogWarning("[TMPFontMaterialFixer] 请先在 Project 中选中一个 TMP Font Asset（如 Baloo-Regular SDF）。");
                return;
            }

            string assetPath = AssetDatabase.GetAssetPath(font);
            string dir = System.IO.Path.GetDirectoryName(assetPath);
            string fontName = font.name;

            // 同目录下查找名称含 "Material" 且与字体名相关的材质（如 "Baloo-Regular SDF Material"）
            string[] guids = AssetDatabase.FindAssets("t:Material", new[] { dir });
            Material targetMat = null;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) continue;
                string matName = mat.name;
                // 匹配：字体名 + " Material" 或 " SDF Material" 等
                if (matName.Contains(fontName) && matName.Contains("Material"))
                {
                    targetMat = mat;
                    break;
                }
                if (targetMat == null && (matName.Contains("SDF") || matName.Contains("Material")))
                    targetMat = mat; // 备选：同目录下任意 SDF/Material
            }

            if (targetMat == null)
            {
                Debug.LogWarning($"[TMPFontMaterialFixer] 在目录 {dir} 下未找到与 \"{fontName}\" 匹配的材质。请手动将材质拖入同目录并命名为「字体名 Material」后重试。");
                return;
            }

            Undo.RecordObject(font, "Assign TMP Font Material");
            font.material = targetMat;
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssetIfDirty(font);
            Debug.Log($"[TMPFontMaterialFixer] 已为 \"{fontName}\" 指定材质: {targetMat.name}");
        }
    }
}
