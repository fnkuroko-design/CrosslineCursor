namespace CrosshairOverlayTool;

internal sealed class SettingsForm : Form
{
    private readonly CheckBox _holdModeCheckBox;
    private readonly ComboBox _holdKeyComboBox;
    private readonly NumericUpDown _eisuPseudoHoldInitialMarginUpDown;
    private readonly CheckBox _horizontalLockCheckBox;
    private readonly HotkeyCaptureTextBox _horizontalLockToggleHotkeyTextBox;
    private readonly CheckBox _toggleModeCheckBox;
    private readonly HotkeyCaptureTextBox _toggleHotkeyTextBox;
    private readonly NumericUpDown _lineThicknessUpDown;
    private readonly NumericUpDown _opacityUpDown;
    private readonly TextBox _lineColorTextBox;
    private readonly Panel _lineColorPreviewPanel;
    private readonly CheckBox _runOnStartupCheckBox;
    private readonly Button _chooseColorButton;
    private readonly Button _resetButton;
    private readonly Button _saveButton;
    private readonly Button _cancelButton;

    public SettingsForm(AppSettings settings)
    {
        Text = $"Crosshair Overlay Tool {AppVersion.DisplayText} 設定";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(580, 560);
        Font = new Font("Yu Gothic UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

        var rootLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 1,
            RowCount = 2
        };
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        Controls.Add(rootLayout);

        var contentPanel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(0, 0, 4, 0)
        };

        var contentLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 3
        };
        contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        contentLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        contentLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        contentLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        contentPanel.Controls.Add(contentLayout);

        _holdModeCheckBox = new CheckBox
        {
            AutoSize = true,
            Text = "ホールドモードを有効にする"
        };

        _holdKeyComboBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 180
        };
        _holdKeyComboBox.Items.AddRange([.. KeyBindingParser.SupportedHoldKeyNames]);
        _holdKeyComboBox.SelectedIndexChanged += OnModeCheckboxChanged;

        _eisuPseudoHoldInitialMarginUpDown = new NumericUpDown
        {
            Minimum = AppSettings.MinEisuPseudoHoldInitialMarginMs,
            Maximum = AppSettings.MaxEisuPseudoHoldInitialMarginMs,
            Width = 80
        };

        _horizontalLockCheckBox = new CheckBox
        {
            AutoSize = true,
            Text = "ホールド中に軸拘束する"
        };

        _horizontalLockToggleHotkeyTextBox = new HotkeyCaptureTextBox
        {
            Width = 220
        };

        _toggleModeCheckBox = new CheckBox
        {
            AutoSize = true,
            Text = "トグルモードを有効にする"
        };

        _toggleHotkeyTextBox = new HotkeyCaptureTextBox
        {
            Width = 220
        };

        var inputGroup = new GroupBox
        {
            Text = "表示条件",
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };
        inputGroup.Controls.Add(BuildInputLayout());

        _lineThicknessUpDown = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 20,
            Width = 80
        };

        _opacityUpDown = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 255,
            Width = 80
        };

        _lineColorTextBox = new TextBox
        {
            Width = 120
        };
        _lineColorTextBox.TextChanged += OnLineColorTextChanged;

        _lineColorPreviewPanel = new Panel
        {
            BorderStyle = BorderStyle.FixedSingle,
            Width = 28,
            Height = 28,
            Margin = new Padding(0, 2, 0, 0)
        };

        _chooseColorButton = new Button
        {
            AutoSize = true,
            Text = "色を選ぶ..."
        };
        _chooseColorButton.Click += OnChooseColorButtonClick;

        var appearanceGroup = new GroupBox
        {
            Text = "見た目",
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };
        appearanceGroup.Controls.Add(BuildAppearanceLayout());

        _runOnStartupCheckBox = new CheckBox
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            Text = "Windows 起動時に自動起動する"
        };

        _saveButton = new Button
        {
            AutoSize = true,
            Text = "保存"
        };
        _saveButton.Click += OnSaveButtonClick;

        _resetButton = new Button
        {
            AutoSize = true,
            Text = "デフォルトに戻す"
        };
        _resetButton.Click += OnResetButtonClick;

        _cancelButton = new Button
        {
            AutoSize = true,
            Text = "キャンセル",
            DialogResult = DialogResult.Cancel
        };

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            WrapContents = false,
            Margin = new Padding(0, 12, 0, 0)
        };
        buttonPanel.Controls.Add(_cancelButton);
        buttonPanel.Controls.Add(_saveButton);
        buttonPanel.Controls.Add(_resetButton);

        contentLayout.Controls.Add(inputGroup, 0, 0);
        contentLayout.Controls.Add(appearanceGroup, 0, 1);
        contentLayout.Controls.Add(_runOnStartupCheckBox, 0, 2);

        rootLayout.Controls.Add(contentPanel, 0, 0);
        rootLayout.Controls.Add(buttonPanel, 0, 1);

        AcceptButton = _saveButton;
        CancelButton = _cancelButton;

        ApplySettingsToControls(settings);
        UpdateControlState();
    }

    public AppSettings? ResultSettings { get; private set; }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _lineColorTextBox.TextChanged -= OnLineColorTextChanged;
            _chooseColorButton.Click -= OnChooseColorButtonClick;
            _resetButton.Click -= OnResetButtonClick;
            _saveButton.Click -= OnSaveButtonClick;
            _holdModeCheckBox.CheckedChanged -= OnModeCheckboxChanged;
            _horizontalLockCheckBox.CheckedChanged -= OnModeCheckboxChanged;
            _toggleModeCheckBox.CheckedChanged -= OnModeCheckboxChanged;
            _holdKeyComboBox.SelectedIndexChanged -= OnModeCheckboxChanged;
        }

        base.Dispose(disposing);
    }

    private Control BuildInputLayout()
    {
        var holdHintLabel = new Label
        {
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(0, 6, 0, 0),
            MaximumSize = new Size(470, 0),
            Text = "CapsLock はロック状態、英数は IME 状態を同時に変えることがあります。"
        };

        var eisuHintLabel = new Label
        {
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(0, 6, 0, 0),
            MaximumSize = new Size(470, 0),
            Text = "英数キーをホールドキーにした場合のみ使用します。小さくすると短押し後に早く消えますが、瞬断しやすくなります。"
        };

        var horizontalLockHintLabel = new Label
        {
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(0, 6, 0, 0),
            MaximumSize = new Size(470, 0),
            Text = "軸拘束はホールド表示中だけ有効です。最初の動きで水平または垂直を自動判定します。有効/無効はトレイメニューまたは下のショートカットでも切り替えられます。"
        };

        var toggleHintLabel = new Label
        {
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(0, 6, 0, 0),
            MaximumSize = new Size(470, 0),
            Text = "入力欄をクリックして、実際に押したキーの組み合わせを記録します。修飾キー + もう 1 キーが必要です。"
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10, 12, 10, 10),
            AutoSize = true,
            ColumnCount = 2
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        _holdModeCheckBox.CheckedChanged += OnModeCheckboxChanged;
        _horizontalLockCheckBox.CheckedChanged += OnModeCheckboxChanged;
        _toggleModeCheckBox.CheckedChanged += OnModeCheckboxChanged;

        layout.Controls.Add(_holdModeCheckBox, 0, 0);
        layout.SetColumnSpan(_holdModeCheckBox, 2);

        layout.Controls.Add(new Label
        {
            AutoSize = true,
            Margin = new Padding(0, 8, 8, 0),
            Text = "ホールドキー"
        }, 0, 1);
        layout.Controls.Add(_holdKeyComboBox, 1, 1);
        layout.Controls.Add(holdHintLabel, 0, 2);
        layout.SetColumnSpan(holdHintLabel, 2);

        layout.Controls.Add(new Label
        {
            AutoSize = true,
            Margin = new Padding(0, 8, 8, 0),
            Text = "英数ホールド初期余白 (0-60ms)"
        }, 0, 3);
        layout.Controls.Add(_eisuPseudoHoldInitialMarginUpDown, 1, 3);
        layout.Controls.Add(eisuHintLabel, 0, 4);
        layout.SetColumnSpan(eisuHintLabel, 2);

        layout.Controls.Add(_horizontalLockCheckBox, 0, 5);
        layout.SetColumnSpan(_horizontalLockCheckBox, 2);

        layout.Controls.Add(new Label
        {
            AutoSize = true,
            Margin = new Padding(0, 8, 8, 0),
            Text = "拘束切替ホットキー"
        }, 0, 6);
        layout.Controls.Add(_horizontalLockToggleHotkeyTextBox, 1, 6);

        layout.Controls.Add(horizontalLockHintLabel, 0, 7);
        layout.SetColumnSpan(horizontalLockHintLabel, 2);

        layout.Controls.Add(_toggleModeCheckBox, 0, 8);
        layout.SetColumnSpan(_toggleModeCheckBox, 2);

        layout.Controls.Add(new Label
        {
            AutoSize = true,
            Margin = new Padding(0, 8, 8, 0),
            Text = "トグルホットキー"
        }, 0, 9);
        layout.Controls.Add(_toggleHotkeyTextBox, 1, 9);

        layout.Controls.Add(toggleHintLabel, 0, 10);
        layout.SetColumnSpan(toggleHintLabel, 2);

        return layout;
    }

    private Control BuildAppearanceLayout()
    {
        var helpLabel = new Label
        {
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(0, 6, 0, 0),
            MaximumSize = new Size(470, 0),
            Text = "色は #RRGGBB 形式で入力できます。不透明度は 255 が最も濃く、1 が最も薄いです。"
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10, 12, 10, 10),
            AutoSize = true,
            ColumnCount = 2
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        layout.Controls.Add(new Label
        {
            AutoSize = true,
            Margin = new Padding(0, 4, 8, 0),
            Text = "線の太さ (1-20)"
        }, 0, 0);
        layout.Controls.Add(_lineThicknessUpDown, 1, 0);

        layout.Controls.Add(new Label
        {
            AutoSize = true,
            Margin = new Padding(0, 10, 8, 0),
            Text = "不透明度 (1-255)"
        }, 0, 1);
        layout.Controls.Add(_opacityUpDown, 1, 1);

        layout.Controls.Add(new Label
        {
            AutoSize = true,
            Margin = new Padding(0, 12, 8, 0),
            Text = "線の色"
        }, 0, 2);

        var colorPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 8, 0, 0),
            WrapContents = false
        };
        colorPanel.Controls.Add(_lineColorTextBox);
        colorPanel.Controls.Add(_lineColorPreviewPanel);
        colorPanel.Controls.Add(_chooseColorButton);

        layout.Controls.Add(colorPanel, 1, 2);

        layout.Controls.Add(helpLabel, 0, 3);
        layout.SetColumnSpan(helpLabel, 2);

        return layout;
    }

    private void ApplySettingsToControls(AppSettings settings)
    {
        _holdModeCheckBox.Checked = settings.EnableHoldMode;
        _holdKeyComboBox.SelectedItem = KeyBindingParser.SupportedHoldKeyNames.Contains(settings.HoldKey)
            ? settings.HoldKey
            : KeyBindingParser.DefaultHoldKeyText;
        _eisuPseudoHoldInitialMarginUpDown.Value = Math.Clamp(
            settings.EisuPseudoHoldInitialMarginMs,
            AppSettings.MinEisuPseudoHoldInitialMarginMs,
            AppSettings.MaxEisuPseudoHoldInitialMarginMs);
        _horizontalLockCheckBox.Checked = settings.EnableHorizontalLock;
        _horizontalLockToggleHotkeyTextBox.Text = settings.HorizontalLockToggleHotkey;
        _toggleModeCheckBox.Checked = settings.EnableToggleMode;
        _toggleHotkeyTextBox.Text = settings.ToggleHotkey;
        _lineThicknessUpDown.Value = Math.Clamp(settings.LineThickness, 1, 20);
        _opacityUpDown.Value = Math.Clamp(settings.Opacity, 1, 255);
        _lineColorTextBox.Text = settings.LineColor;
        _runOnStartupCheckBox.Checked = settings.RunOnStartup;
        UpdateColorPreview();
    }

    private void OnModeCheckboxChanged(object? sender, EventArgs e)
    {
        UpdateControlState();
    }

    private void UpdateControlState()
    {
        var holdModeEnabled = _holdModeCheckBox.Checked;

        _holdKeyComboBox.Enabled = holdModeEnabled;
        _horizontalLockCheckBox.Enabled = holdModeEnabled;
        _horizontalLockToggleHotkeyTextBox.Enabled = holdModeEnabled;
        _eisuPseudoHoldInitialMarginUpDown.Enabled = holdModeEnabled
            && string.Equals(_holdKeyComboBox.SelectedItem as string, "英数", StringComparison.Ordinal);
        _toggleHotkeyTextBox.Enabled = _toggleModeCheckBox.Checked;
    }

    private void OnLineColorTextChanged(object? sender, EventArgs e)
    {
        UpdateColorPreview();
    }

    private void UpdateColorPreview()
    {
        _lineColorPreviewPanel.BackColor = ColorUtilities.TryParseHtmlColor(_lineColorTextBox.Text, out var color)
            ? color
            : SystemColors.ControlDark;
    }

    private void OnChooseColorButtonClick(object? sender, EventArgs e)
    {
        var initialColor = ColorUtilities.TryParseHtmlColor(_lineColorTextBox.Text, out var parsedColor)
            ? parsedColor
            : ColorTranslator.FromHtml(ColorUtilities.DefaultLineColorHex);

        using var dialog = new ColorDialog
        {
            FullOpen = true,
            SolidColorOnly = true,
            Color = initialColor
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _lineColorTextBox.Text = ColorUtilities.ToHtml(dialog.Color);
    }

    private void OnResetButtonClick(object? sender, EventArgs e)
    {
        var result = MessageBox.Show(
            this,
            "現在の入力内容を破棄して、すべての項目をデフォルト値に戻しますか？",
            "デフォルト値に戻す",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (result != DialogResult.Yes)
        {
            return;
        }

        ApplySettingsToControls(new AppSettings());
        UpdateControlState();
    }

    private void OnSaveButtonClick(object? sender, EventArgs e)
    {
        if (_holdKeyComboBox.SelectedItem is not string holdKey)
        {
            ShowValidationError("ホールドキーを選択してください。");
            return;
        }

        var toggleHotkey = _toggleHotkeyTextBox.Text.Trim();
        if (!KeyBindingParser.TryParseHotkey(toggleHotkey, out var hotkeyBinding))
        {
            ShowValidationError("トグルホットキーの書式が正しくありません。");
            return;
        }

        var horizontalLockToggleHotkey = _horizontalLockToggleHotkeyTextBox.Text.Trim();
        if (!KeyBindingParser.TryParseHotkey(horizontalLockToggleHotkey, out var horizontalLockHotkeyBinding))
        {
            ShowValidationError("拘束切替ホットキーの書式が正しくありません。");
            return;
        }

        if (string.Equals(hotkeyBinding.DisplayText, horizontalLockHotkeyBinding.DisplayText, StringComparison.Ordinal))
        {
            ShowValidationError("トグルホットキーと拘束切替ホットキーには異なる組み合わせを指定してください。");
            return;
        }

        if (!ColorUtilities.TryParseHtmlColor(_lineColorTextBox.Text, out var color))
        {
            ShowValidationError("線の色は #RRGGBB 形式で入力してください。");
            return;
        }

        ResultSettings = new AppSettings
        {
            EnableHoldMode = _holdModeCheckBox.Checked,
            EnableToggleMode = _toggleModeCheckBox.Checked,
            HoldKey = holdKey,
            ToggleHotkey = hotkeyBinding.DisplayText,
            HorizontalLockToggleHotkey = horizontalLockHotkeyBinding.DisplayText,
            LineThickness = (int)_lineThicknessUpDown.Value,
            Opacity = (int)_opacityUpDown.Value,
            LineColor = ColorUtilities.ToHtml(color),
            RunOnStartup = _runOnStartupCheckBox.Checked,
            EisuPseudoHoldInitialMarginMs = (int)_eisuPseudoHoldInitialMarginUpDown.Value,
            EnableHorizontalLock = _horizontalLockCheckBox.Checked,
            HorizontalLockTriggerMode = AppSettings.HorizontalLockTriggerModeSameHoldKey,
            HorizontalLockHoldKey = holdKey
        };

        DialogResult = DialogResult.OK;
        Close();
    }

    private void ShowValidationError(string message)
    {
        MessageBox.Show(this, message, "入力エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }
}
