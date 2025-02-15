using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using System.Collections.Generic;
using System.IO;

namespace UABEAvalonia
{
    public partial class AddAssetWindow : Window
    {
        //controls
        private ComboBox ddFileId;
        private TextBox boxPathId;
        private TextBox boxTypeId;
        private TextBox boxMonoId;
        private CheckBox chkCreateZerodAsset;
        private Button btnOk;
        private Button btnCancel;

        private AssetWorkspace workspace;

        public AddAssetWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            //generated controls
            ddFileId = this.FindControl<ComboBox>("ddFileId");
            boxPathId = this.FindControl<TextBox>("boxPathId");
            boxTypeId = this.FindControl<TextBox>("boxTypeId");
            boxMonoId = this.FindControl<TextBox>("boxMonoId");
            chkCreateZerodAsset = this.FindControl<CheckBox>("chkCreateZerodAsset");
            btnOk = this.FindControl<Button>("btnOk");
            btnCancel = this.FindControl<Button>("btnCancel");
            //generated events
            btnOk.Click += BtnOk_Click;
            btnCancel.Click += BtnCancel_Click;
            boxPathId.KeyDown += TextBoxKeyDown;
            boxTypeId.KeyDown += TextBoxKeyDown;
            boxMonoId.KeyDown += TextBoxKeyDown;
        }

        public AddAssetWindow(AssetWorkspace workspace) : this()
        {
            this.workspace = workspace;

            int index = 0;
            List<string> loadedFiles = new List<string>();
            foreach (AssetsFileInstance inst in workspace.LoadedFiles)
            {
                loadedFiles.Add($"{index++} - {Path.GetFileName(inst.path)}");
            }
            ddFileId.Items = loadedFiles;
            ddFileId.SelectedIndex = 0;
            boxPathId.Text = "1"; //todo get last id (including new assets)
            boxTypeId.Text = "1";
            boxMonoId.Text = "-1";
        }

        private void BtnOk_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            ReturnAssetToAdd();
        }

        private void BtnCancel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Close(false);
        }

        private void TextBoxKeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                ReturnAssetToAdd();
            }
        }

        private async void ReturnAssetToAdd()
        {
            int fileId = ddFileId.SelectedIndex; //hopefully in order
            string pathIdText = boxPathId.Text;
            string typeIdText = boxTypeId.Text;
            string monoIdText = boxMonoId.Text;
            bool createBlankAsset = chkCreateZerodAsset.IsChecked ?? false;

            AssetsFileInstance file = workspace.LoadedFiles[fileId];

            AssetTypeTemplateField tempField;
            byte[] assetBytes;
            long pathId;
            int typeId;
            ushort monoId;

            if (!long.TryParse(pathIdText, out pathId))
            {
                await MessageBoxUtil.ShowDialog(this, "Bad input", "Path ID was invalid.");
                return;
            }

            if (file.file.Metadata.TypeTreeEnabled)
            {
                if (!TryParseTypeTree(file, typeIdText, createBlankAsset, out tempField, out typeId))
                {
                    if (!TryParseClassDatabase(typeIdText, createBlankAsset, out tempField, out typeId))
                    {
                        await MessageBoxUtil.ShowDialog(this, "Bad input", "Class type was invalid.");
                        return;
                    }
                    else
                    {
                        //has typetree but had to lookup to cldb
                        //we need to add a new typetree entry because this is
                        //probably not a type that existed in this bundle
                        file.file.Metadata.TypeTreeTypes.Add(ClassDatabaseToTypeTree.Convert(workspace.am.classDatabase, typeId));
                    }
                }
            }
            else
            {
                if (!TryParseClassDatabase(typeIdText, createBlankAsset, out tempField, out typeId))
                {
                    await MessageBoxUtil.ShowDialog(this, "Bad input", "Class type was invalid.");
                    return;
                }
            }

            if (monoIdText == "-1")
            {
                monoId = 0xffff;
            }
            else if (!ushort.TryParse(monoIdText, out monoId))
            {
                await MessageBoxUtil.ShowDialog(this, "Bad input", "Mono ID was invalid.");
                return;
            }

            if (createBlankAsset)
            {
                AssetTypeValueField baseField = ValueBuilder.DefaultValueFieldFromTemplate(tempField);
                assetBytes = baseField.WriteToByteArray();
            }
            else
            {
                assetBytes = new byte[0];
            }

            workspace.AddReplacer(file, new AssetsReplacerFromMemory(0, pathId, typeId, monoId, assetBytes), new MemoryStream(assetBytes));

            Close(true);
        }

        private bool TryParseClassDatabase(string typeIdText, bool createBlankAsset, out AssetTypeTemplateField tempField, out int typeId)
        {
            tempField = null;

            ClassDatabaseFile cldb = workspace.am.classDatabase;
            ClassDatabaseType cldbType;
            bool needsTypeId;
            if (int.TryParse(typeIdText, out typeId))
            {
                cldbType = AssetHelper.FindAssetClassByID(cldb, typeId);
                needsTypeId = false;
            }
            else
            {
                cldbType = AssetHelper.FindAssetClassByName(cldb, typeIdText);
                needsTypeId = true;
            }

            if (cldbType == null)
            {
                return false;
            }

            if (needsTypeId)
            {
                typeId = cldbType.ClassId;
            }

            if (createBlankAsset)
            {
                tempField = new AssetTypeTemplateField();
                tempField.FromClassDatabase(cldb, cldbType);
            }
            return true;
        }

        private bool TryParseTypeTree(AssetsFileInstance file, string typeIdText, bool createBlankAsset, out AssetTypeTemplateField tempField, out int typeId)
        {
            tempField = null;

            AssetsFileMetadata meta = file.file.Metadata;
            TypeTreeType ttType;
            bool needsTypeId;
            if (int.TryParse(typeIdText, out typeId))
            {
                ttType = AssetHelper.FindTypeTreeTypeByID(meta, typeId);
                needsTypeId = false;
            }
            else
            {
                ttType = AssetHelper.FindTypeTreeTypeByName(meta, typeIdText);
                needsTypeId = true;
            }

            if (ttType == null)
            {
                return false;
            }

            if (needsTypeId)
            {
                typeId = ttType.TypeId;
            }

            if (createBlankAsset)
            {
                tempField = new AssetTypeTemplateField();
                tempField.FromTypeTree(ttType);
            }
            return true;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
