// 全局 using 声明（csproj 已关闭隐式 using，这里显式列出，行为完全可预期）
// 注意：刻意不引入 System.Windows.Forms，避免 Application / MessageBox 等类型歧义，
//      托盘相关的 WinForms 类型一律使用完全限定名。
global using System;
global using System.Collections.Generic;
global using System.Collections.ObjectModel;
global using System.ComponentModel;
global using System.Diagnostics;
global using System.Globalization;
global using System.IO;
global using System.Linq;
global using System.Net.Http;
global using System.Text.Json;
global using System.Threading;
global using System.Threading.Tasks;
global using System.Windows;
global using System.Windows.Controls;
global using System.Windows.Input;
global using System.Windows.Media;
global using System.Windows.Threading;
