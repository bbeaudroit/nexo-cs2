!include "MUI2.nsh"
!include "nsDialogs.nsh"
!ifndef BUILD_UNINSTALLER
Var NexoHideCheckbox
Var NexoHideFolder
!endif
!macro customWelcomePage
  !define MUI_WELCOMEPAGE_TITLE "Welcome to Nexo"
  !define MUI_WELCOMEPAGE_TEXT "Choose where to install Nexo.$\r$\n$\r$\nYour CFGs stay in your Windows user profile.$\r$\n$\r$\nNexo will not open when setup finishes. Open it when you are ready; it will wait for CS2."
  !insertmacro MUI_PAGE_WELCOME
!macroend
!macro customPageAfterChangeDir
  Page custom NexoOptionsPage NexoOptionsLeave
!macroend
!ifndef BUILD_UNINSTALLER
Function NexoOptionsPage
  ${If} ${Silent}
    Abort
  ${EndIf}
  !insertmacro MUI_HEADER_TEXT "Installation options" "Choose how the Nexo folder appears in Windows."
  nsDialogs::Create 1018
  Pop $0
  ${If} $0 == error
    Abort
  ${EndIf}
  ${NSD_CreateCheckbox} 0 12u 100% 20u "Mark the Nexo installation folder as hidden"
  Pop $NexoHideCheckbox
  ${If} $NexoHideFolder == ${BST_CHECKED}
    ${NSD_Check} $NexoHideCheckbox
  ${EndIf}
  ${NSD_CreateLabel} 0 45u 100% 60u "Optional. Windows Explorer hides this folder unless Hidden items is enabled. Shortcuts still work. This does not protect files or affect detection by other programs. Your CFG folder stays unchanged."
  Pop $0
  nsDialogs::Show
FunctionEnd
Function NexoOptionsLeave
  ${NSD_GetState} $NexoHideCheckbox $NexoHideFolder
FunctionEnd
!endif
!macro customInstall
  ${If} $NexoHideFolder == ${BST_CHECKED}
    IfFileExists "$INSTDIR\nexo-install.json" 0 nexo_hide_done
    IfFileExists "$INSTDIR\Nexo.exe" 0 nexo_hide_done
    System::Call 'kernel32::GetFileAttributesW(w "$INSTDIR") i .r0'
    ${If} $0 != -1
      IntOp $0 $0 | 2
      System::Call 'kernel32::SetFileAttributesW(w "$INSTDIR", i r0) i .r1'
      ${If} $1 == 0
        DetailPrint "The folder could not be hidden; installation remains usable."
      ${EndIf}
    ${EndIf}
    nexo_hide_done:
  ${EndIf}
!macroend


