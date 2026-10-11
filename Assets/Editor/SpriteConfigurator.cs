using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SlotGame.EditorTools
{
    /// <summary>
    /// Applies the correct TextureImporter settings to every piece of source art:
    ///
    ///  - Machine parts and symbols are pixel art: Point filtering, no mipmaps,
    ///    uncompressed (so WebGL compression never smears the crisp pixels).
    ///  - UI panels/buttons are smooth gradients: Bilinear filtering.
    ///  - Button sheets are sliced into named state sprites (Normal / Highlighted /
    ///    Pressed / Disabled) so Unity's Button sprite-swap can use them directly.
    ///
    /// Slice rectangles were measured from the source PNGs with a pixel-analysis
    /// script, which guarantees pixel-perfect cells.
    /// </summary>
    public static class SpriteConfigurator
    {
        // Round button sheets: 256x1024, four 256x256 states stacked top-to-bottom.
        private const float RoundCell = 256f;
        private static readonly string[] RoundStates = { "Normal", "Highlighted", "Pressed", "Disabled" };

        /// <summary>Configures every sprite in the project. Idempotent.</summary>
        public static void ConfigureAll()
        {
            // Pixel art, single sprite.
            foreach (string path in new[]
            {
                AssetPaths.MachineBody, AssetPaths.LeverUp, AssetPaths.LeverDown,
                AssetPaths.GlassOverlay, AssetPaths.SymbolSeven, AssetPaths.SymbolCherry,
                AssetPaths.SymbolBell, AssetPaths.SymbolBar
            })
            {
                ConfigureSingle(path, FilterMode.Point);
            }

            // Smooth art, single sprite.
            foreach (string path in new[]
            {
                AssetPaths.Background, AssetPaths.PopupPanel, AssetPaths.DisplayBox
            })
            {
                ConfigureSingle(path, FilterMode.Bilinear);
            }

            // Round button sheets (X, left arrow, right arrow).
            ConfigureRoundSheet(AssetPaths.ButtonCloseSheet, "Close");
            ConfigureRoundSheet(AssetPaths.ButtonLeftSheet, "Left");
            ConfigureRoundSheet(AssetPaths.ButtonRightSheet, "Right");

            // Yes/No sheet: 990x690, 2 columns (YES, NO) x 3 rows (Normal, Hover, Pressed).
            ConfigureYesNoSheet();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[SlotGame] Sprite configuration complete.");
        }

        private static void ConfigureSingle(string path, FilterMode filterMode)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning("[SlotGame] Texture not found: " + path);
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            ApplyCommon(importer, filterMode);
        }

        private static void ConfigureRoundSheet(string path, string prefix)
        {
            var cells = new List<SpriteMetaData>();
            for (int i = 0; i < RoundStates.Length; i++)
            {
                cells.Add(new SpriteMetaData
                {
                    name = prefix + "_" + RoundStates[i],
                    // Texture Y is measured from the bottom; state 0 (Normal) is at the top.
                    rect = new Rect(0f, RoundCell * (RoundStates.Length - 1 - i), RoundCell, RoundCell),
                    pivot = new Vector2(0.5f, 0.5f),
                    alignment = (int)SpriteAlignment.Center
                });
            }
            ConfigureSheet(path, cells.ToArray());
        }

        private static void ConfigureYesNoSheet()
        {
            const float cellW = 495f, cellH = 230f;
            var cells = new List<SpriteMetaData>();
            string[] cols = { "Yes", "No" };
            string[] rows = { "Normal", "Highlighted", "Pressed" };
            for (int c = 0; c < cols.Length; c++)
            {
                for (int r = 0; r < rows.Length; r++)
                {
                    cells.Add(new SpriteMetaData
                    {
                        name = cols[c] + "_" + rows[r],
                        rect = new Rect(cellW * c, 690f - cellH * (r + 1), cellW, cellH),
                        pivot = new Vector2(0.5f, 0.5f),
                        alignment = (int)SpriteAlignment.Center
                    });
                }
            }
            ConfigureSheet(AssetPaths.ButtonYesNoSheet, cells.ToArray());
        }

        private static void ConfigureSheet(string path, SpriteMetaData[] cells)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning("[SlotGame] Texture not found: " + path);
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritesheet = cells;
            ApplyCommon(importer, FilterMode.Bilinear);
        }

        private static void ApplyCommon(TextureImporter importer, FilterMode filterMode)
        {
            importer.filterMode = filterMode;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
    }
}
