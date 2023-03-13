using System;
using System.Windows.Forms;
using System.Reflection;
using TagTool.Common;
using TagTool.Cache;
using TagTool.Tags;
using TagTool.Commands;
using Sentinel.Forms;
using System.Drawing;

namespace Sentinel.Controls
{
    public partial class TagReferenceControl : UserControl, IFieldControl
    {
        public CacheForm Form { get; }
        public GameCache Cache { get; }
        public FieldInfo Field { get; }
        public object Owner { get; set; } = null;
        public bool Loading { get; set; } = false;

        public TagReferenceControl()
        {
            InitializeComponent();
        }

        public TagReferenceControl(CacheForm form, GameCache cache, FieldInfo field) :
            this()
        {
            Form = form;
            Cache = cache;
            Field = field;
            label1.Text = field.Name.ToSpaced();

            new ToolTip().SetToolTip(label1, $"{field.FieldType.Name} {CacheForm.GetDocumentation(field)}");
        }

        public void GetFieldValue(object owner, object value = null, object definition = null)
        {
            if (value == null)
                value = Field.GetValue(owner);

            Owner = owner;
            Loading = true;

            if (value == null)
            {
                textBox.Text = "";
                return;
            }

            var tag = (CachedTag)value;

            var tagName = tag.Name ?? $"0x{tag.Index:X4}";

            textBox.Text = $"{tagName}.{Cache.StringTable.GetString(tag.Group.Name)}";

            Loading = false;
        }

        private void browseButton_Click(object sender, EventArgs e)
        {
            using (var td = new TagDialog(Cache))
            {
                if (td.ShowDialog() != DialogResult.OK)
                    return;

                var tag = td.Value;

                var tagName = tag.Name ?? $"0x{tag.Index:X4}";

                textBox.Text = $"{tagName}.{Cache.StringTable.GetString(tag.ID)}";
            }
        }

        private void textBox_TextChanged(object sender, EventArgs e)
        {
            GetFieldValue(Owner);
        }

        private void renameReferencedTag_Click(object sender, EventArgs e)
        {
            if (Owner == null)
                return;

            GetFieldValue(Owner);
            var tag = (CachedTag)Field.GetValue(Owner);

            Form.RenameTag(tag);

            GetFieldValue(Owner);
        }

        public void SetFieldValue(object owner, object value = null, object definition = null)
        {
            throw new NotImplementedException();
        }
    }
}