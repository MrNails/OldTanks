using System.Runtime.CompilerServices;
using ImGuiNET;

namespace OldTanks.UI.ImGuiControls
{
    public enum CharsDisplayForm
    {
        Decimal,
        Hexadecimal,
        Scientific,
        Password
    }
    
    public class ImGuiTextBox : ImGuiControl
    {
        private string m_text;
        private uint m_maxLength;
        
        private CharsDisplayForm m_charsDisplayForm;
        
        private ImGuiInputTextFlags m_flags;

        public ImGuiTextBox(string name) : base(name)
        {
            MaxLength = 255;
            m_text = string.Empty;

            m_flags = ImGuiInputTextFlags.None;
        }

        public string Text
        {
            get => m_text;
            set
            {
                if (m_text == value)
                    return;
                
                m_text = value ?? string.Empty;
                OnPropertyChanged();
            }
        }

        public uint MaxLength
        {
            get => m_maxLength;
            set => SetField(ref m_maxLength, value);
        }

        public bool IsPassword
        {
            get => m_flags.HasFlag(ImGuiInputTextFlags.Password);
            set => SetFlagAndNotifyIfChanged(ImGuiInputTextFlags.Password, value);
        }

        public bool IsReadOnly
        {
            get => m_flags.HasFlag(ImGuiInputTextFlags.ReadOnly);
            set => SetFlagAndNotifyIfChanged(ImGuiInputTextFlags.ReadOnly, value);
        }

        public bool DisableUndoRedo
        {
            get => m_flags.HasFlag(ImGuiInputTextFlags.NoUndoRedo);
            set => SetFlagAndNotifyIfChanged(ImGuiInputTextFlags.NoUndoRedo, value);
        }
        
        public bool UseUpperCase 
        {
            get => m_flags.HasFlag(ImGuiInputTextFlags.CharsUppercase);
            set => SetFlagAndNotifyIfChanged(ImGuiInputTextFlags.CharsUppercase, value);
        }
        
        public bool AllowNewLine
        {
            get => m_flags.HasFlag(ImGuiInputTextFlags.CtrlEnterForNewLine);
            set => SetFlagAndNotifyIfChanged(ImGuiInputTextFlags.CtrlEnterForNewLine, value);
        }
        
        public bool AllowTab
        {
            get => m_flags.HasFlag(ImGuiInputTextFlags.AllowTabInput);
            set => SetFlagAndNotifyIfChanged(ImGuiInputTextFlags.AllowTabInput, value);
        }
        
        public bool ClearAllOnEscape
        {
            get => m_flags.HasFlag(ImGuiInputTextFlags.EscapeClearsAll);
            set => SetFlagAndNotifyIfChanged(ImGuiInputTextFlags.EscapeClearsAll, value);
        }

        public CharsDisplayForm CharsDisplayForm
        {
            get => m_charsDisplayForm;
            set
            {
                if (value == m_charsDisplayForm) 
                    return;
                
                m_charsDisplayForm = value;
                OnPropertyChanged();
            }
        }

        public override void Draw()
        {
            if (!IsVisible)
                return;
            
            base.Draw();
            ImGui.InputText(Name, ref m_text, m_maxLength, m_flags);
        }

        private void SetFlagAndNotifyIfChanged(ImGuiInputTextFlags flag, bool newValue, [CallerMemberName]string callerName = "")
        {
            var hasFlag = m_flags.HasFlag(ImGuiInputTextFlags.Password);

            if (hasFlag && newValue || 
                !hasFlag && !newValue)
            {
                return;
            }

            if (newValue)
                m_flags |= flag;
            else
                m_flags &= flag;
            
            OnPropertyChanged(callerName);
        }

        private void SetFlagsFromCharsDisplayForm(CharsDisplayForm newValue, CharsDisplayForm oldValue)
        {
            m_flags |= newValue switch
            {
                CharsDisplayForm.Decimal => ImGuiInputTextFlags.CharsDecimal,
                CharsDisplayForm.Hexadecimal => ImGuiInputTextFlags.CharsHexadecimal,
                CharsDisplayForm.Scientific => ImGuiInputTextFlags.CharsScientific,
                CharsDisplayForm.Password => ImGuiInputTextFlags.Password,
                _ => throw new ArgumentOutOfRangeException(nameof(newValue), newValue,
                    $"CharsDisplayForm have unhandled value {newValue}")
            };

            m_flags &= oldValue switch
            {
                CharsDisplayForm.Decimal => ImGuiInputTextFlags.CharsDecimal,
                CharsDisplayForm.Hexadecimal => ImGuiInputTextFlags.CharsHexadecimal,
                CharsDisplayForm.Scientific => ImGuiInputTextFlags.CharsScientific,
                CharsDisplayForm.Password => ImGuiInputTextFlags.Password,
                _ => throw new ArgumentOutOfRangeException(nameof(oldValue), oldValue,
                    $"CharsDisplayForm have unhandled value {oldValue}")
            };
        }
    }
}
