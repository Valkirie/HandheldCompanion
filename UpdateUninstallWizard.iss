[Code]    
var
  UninstallFirstPage: TNewNotebookPage;
  UninstallSecondPage: TNewNotebookPage;
  UninstallBackButton: TNewButton;
  UninstallNextButton: TNewButton;
  checkListBox: TNewCheckListBox;
  keepAllCheck, profilesCheck, hotkeysCheck, applicationSettingsCheck:integer;
  keepVigemCheckbox, keepHidHideCheckbox: TNewCheckBox;

procedure UpdateUninstallWizard;
begin
  if UninstallProgressForm.InnerNotebook.ActivePage = UninstallFirstPage then
  begin
    UninstallProgressForm.PageNameLabel.Caption := 'Select Settings To Be Kept';
    UninstallProgressForm.PageDescriptionLabel.Caption := 'Which items are to be kept?';
  end
  else
  if UninstallProgressForm.InnerNotebook.ActivePage = UninstallSecondPage then
  begin
    UninstallProgressForm.PageNameLabel.Caption := 'Select Dependency Applications To Be Kept';
    UninstallProgressForm.PageDescriptionLabel.Caption := 'Which applications should be kept?';
  end;
  
  UninstallBackButton.Visible:= (UninstallProgressForm.InnerNotebook.ActivePage <> UninstallFirstPage);

  if UninstallProgressForm.InnerNotebook.ActivePage <> UninstallSecondPage then
  begin
    UninstallNextButton.Caption := 'Next';
    UninstallNextButton.ModalResult := mrNone;
  end
    else
  begin
    UninstallNextButton.Caption := 'Uninstall';
    { Make the "Uninstall" button break the ShowModal loop }
    UninstallNextButton.ModalResult := mrOK;
  end;     
end;  

procedure UninstallNextButtonClick(Sender: TObject);
begin
  if UninstallProgressForm.InnerNotebook.ActivePage = UninstallSecondPage then
  begin
    UninstallNextButton.Visible := False;
    UninstallBackButton.Visible := False;
  end
    else
  begin
    if UninstallProgressForm.InnerNotebook.ActivePage = UninstallFirstPage then
    begin
      UninstallProgressForm.InnerNotebook.ActivePage := UninstallSecondPage;
    end;
    UpdateUninstallWizard;
  end;
end;

procedure UninstallBackButtonClick(Sender: TObject);
begin
  if UninstallProgressForm.InnerNotebook.ActivePage = UninstallSecondPage then
  begin
    UninstallProgressForm.InnerNotebook.ActivePage := UninstallFirstPage;
  end;
  UpdateUninstallWizard;
end;

procedure onClickCheckListBox(Sender: TObject);   
begin

end;
       

procedure InitializeUninstallProgressForm();
var
  PageText: TNewStaticText;
  PageNameLabel: string;
  PageDescriptionLabel: string;
  CancelButtonEnabled: Boolean;
  CancelButtonModalResult: Integer;   
begin
  if not UninstallSilent then
  begin
    { Create the first page and make it active }
    UninstallFirstPage := TNewNotebookPage.Create(UninstallProgressForm);
    UninstallFirstPage.Notebook := UninstallProgressForm.InnerNotebook;
    UninstallFirstPage.Parent := UninstallProgressForm.InnerNotebook;
    UninstallFirstPage.Align := alClient;       
  
    PageText := TNewStaticText.Create(UninstallProgressForm);
    PageText.Parent := UninstallFirstPage;
    PageText.Top := UninstallProgressForm.StatusLabel.Top;
    PageText.Left := UninstallProgressForm.StatusLabel.Left;
    PageText.Width := UninstallProgressForm.StatusLabel.Width;
    PageText.Height := UninstallProgressForm.StatusLabel.Height;
    PageText.AutoSize := False;
    PageText.ShowAccelChar := False;
    PageText.Caption := 'Press Next to proceed with uninstallation.'; 

    checkListBox := TNewCheckListBox.Create(UninstallFirstPage);
    checkListBox.Parent := UninstallFirstPage;
    checkListBox.Left := UninstallProgressForm.StatusLabel.Left;
    checkListBox.Top := PageText.Top + PageText.height + 16;
    checkListBox.Width := 450;
    checkListBox.Height := ScaleY(97);
    checkListBox.Flat := True;
    keepAllCheck:= checkListBox.AddCheckBox('Keep all', '', 0, True, True, True, True, nil);
    profilesCheck:= checkListBox.AddCheckBox('Keep profiles', '', 1, True, True, False, True, nil);
    hotkeysCheck:= checkListBox.AddCheckBox('Keep hotkeys', '', 1, True, True, False,True, nil);
    applicationSettingsCheck:= checkListBox.AddCheckBox('Keep settings', '', 1, True, True, False,True, nil);
    checkListBox.onClick:= @onClickCheckListBox;   
  
    UninstallProgressForm.InnerNotebook.ActivePage := UninstallFirstPage;    

    PageNameLabel := UninstallProgressForm.PageNameLabel.Caption;
    PageDescriptionLabel := UninstallProgressForm.PageDescriptionLabel.Caption;
  
    { Create the second page } 
    UninstallSecondPage := TNewNotebookPage.Create(UninstallProgressForm);
    UninstallSecondPage.Notebook := UninstallProgressForm.InnerNotebook;
    UninstallSecondPage.Parent := UninstallProgressForm.InnerNotebook;
    UninstallSecondPage.Align := alClient;
  
    PageText := TNewStaticText.Create(UninstallProgressForm);
    PageText.Parent := UninstallSecondPage;
    PageText.Top := UninstallProgressForm.StatusLabel.Top;
    PageText.Left := UninstallProgressForm.StatusLabel.Left;
    PageText.Width := UninstallProgressForm.StatusLabel.Width;
    PageText.Height := UninstallProgressForm.StatusLabel.Height;
    PageText.AutoSize := False;
    PageText.ShowAccelChar := False;
    PageText.Caption := 'Press Uninstall to proceeed with uninstallation.';   
    
    keepVigemCheckbox := TNewCheckBox.Create(UninstallProgressForm);
    keepVigemCheckbox.Parent := UninstallSecondPage;
    keepVigemCheckbox.Left := UninstallProgressForm.StatusLabel.Left;
    keepVigemCheckbox.Top := PageText.Top + PageText.height + 16;
    keepVigemCheckbox.Caption := 'Keep Vigem';
    keepVigemCheckbox.checked:= false;

    keepHidhideCheckbox := TNewCheckBox.Create(UninstallProgressForm);
    keepHidhideCheckbox.Parent := UninstallSecondPage;
    keepHidhideCheckbox.Left := UninstallProgressForm.StatusLabel.Left;
    keepHidhideCheckbox.Top := keepVigemCheckbox.Top + PageText.height + 6;
    keepHidhideCheckbox.Caption := 'Keep HidHide';
    keepHidhideCheckbox.checked := false;
  
    UninstallNextButton := TNewButton.Create(UninstallProgressForm);
    UninstallNextButton.Parent := UninstallProgressForm;
    UninstallNextButton.Left :=
      UninstallProgressForm.CancelButton.Left -
      UninstallProgressForm.CancelButton.Width -
      ScaleX(10);
    UninstallNextButton.Top := UninstallProgressForm.CancelButton.Top;
    UninstallNextButton.Width := UninstallProgressForm.CancelButton.Width;
    UninstallNextButton.Height := UninstallProgressForm.CancelButton.Height;
    UninstallNextButton.OnClick := @UninstallNextButtonClick;

    UninstallBackButton := TNewButton.Create(UninstallProgressForm);
    UninstallBackButton.Parent := UninstallProgressForm;
    UninstallBackButton.Left :=
      UninstallNextButton.Left - UninstallNextButton.Width -
      ScaleX(10);
    UninstallBackButton.Top := UninstallProgressForm.CancelButton.Top;
    UninstallBackButton.Width := UninstallProgressForm.CancelButton.Width;
    UninstallBackButton.Height := UninstallProgressForm.CancelButton.Height;
    UninstallBackButton.Caption := SetupMessage(msgButtonBack);
    UninstallBackButton.OnClick := @UninstallBackButtonClick;
    UninstallBackButton.TabOrder := UninstallProgressForm.CancelButton.TabOrder; 
    UninstallNextButton.TabOrder := UninstallBackButton.TabOrder + 1;

    UninstallProgressForm.CancelButton.TabOrder :=
    UninstallNextButton.TabOrder + 1;

    { Run our wizard pages } 
    UpdateUninstallWizard;
    CancelButtonEnabled := UninstallProgressForm.CancelButton.Enabled
    UninstallProgressForm.CancelButton.Enabled := True;
    CancelButtonModalResult := UninstallProgressForm.CancelButton.ModalResult;
    UninstallProgressForm.CancelButton.ModalResult := mrCancel;
    UninstallProgressForm.ActiveControl := UninstallNextButton;

    if UninstallProgressForm.ShowModal = mrCancel then 
      Abort;

    { Restore the standard page payout }
    UninstallProgressForm.CancelButton.Enabled := CancelButtonEnabled;
    UninstallProgressForm.CancelButton.ModalResult := CancelButtonModalResult;

    UninstallProgressForm.PageNameLabel.Caption := PageNameLabel;
    UninstallProgressForm.PageDescriptionLabel.Caption := PageDescriptionLabel;

    UninstallProgressForm.InnerNotebook.ActivePage := UninstallProgressForm.InstallingPage;
  end;
end;