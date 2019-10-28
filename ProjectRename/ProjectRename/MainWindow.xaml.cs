using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ProjectRename
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public int ModifyFileContentCount; //重命名的文件内容数量
        public int ModifyFileNameCount; //重命名文件名称的数量
        public int ModifyDirectoryCount; //重命名目录数量
        public static string SolutionSuffix= ".sln"; //解决方案后缀
        public static string IgnoreDirectories= ".vs,.vscode,.git,obj,bin,debug"; //忽略目录

        public MainWindow()
        {
            InitializeComponent();
        }


        /// <summary>
        /// 拖入
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Txt_PreviewDropOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                //e.Effects = DragDropEffects.Link;
                //e.Handled = true;

                TextBox t = this.FindName("txtProjectPath") as TextBox;
                t.Text = ((System.Array) e.Data.GetData(DataFormats.FileDrop)).GetValue(0).ToString();
            }
            else
                e.Effects = DragDropEffects.None;
        }


        /// <summary>
        /// 拖放结束【没什么用，不会触发】
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Txt_PreviewDrop(object sender, DragEventArgs e)
        {
            TextBox txtProjectPath = this.FindName("txtProjectPath") as TextBox;
            txtProjectPath.Text = ((System.Array)e.Data.GetData(DataFormats.FileDrop)).GetValue(0).ToString();
        }


        /// <summary>
        /// 确定
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnStart_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                InitCount();
                var path = (this.FindName("txtProjectPath") as TextBox)?.Text;
                var newName = (this.FindName("txtNewName") as TextBox)?.Text;
                if (string.IsNullOrWhiteSpace(path))
                {
                    MessageBox.Show("项目路径不能为空！");
                    return;
                }
                if (string.IsNullOrWhiteSpace(newName))
                {
                    MessageBox.Show("新项目名称不能为空！");
                    return;
                }
                if (Directory.Exists(path))
                {
                    var targetInfo = new DirectoryInfo(path);
                    var oldName = path.Substring(path.LastIndexOf("\\") + 1);
                    RenameFileAndDirectoriesNames(targetInfo, oldName, newName);
                    RenameDirectory(targetInfo, oldName, newName); //项目根目录文件夹名称也要修改
                }
                else if (File.Exists(path) && System.IO.Path.GetExtension(path) == SolutionSuffix)
                {
                    var oldName = path.Substring(path.LastIndexOf("\\") + 1).Replace(SolutionSuffix, "");
                    var targetInfo = new DirectoryInfo(GetDirectoryPathByFile(path));
                    RenameFileAndDirectoriesNames(targetInfo, oldName, newName);
                    RenameDirectory(targetInfo, oldName, newName); //项目根目录文件夹名称也要修改
                }
                else
                {
                    MessageBox.Show("路径不存在！");
                    return;
                }
                
                var msg = (ModifyFileContentCount + ModifyFileNameCount + ModifyDirectoryCount) > 0
                    ? $"重命名结束，本次修改文件如下。\n\r修改的文件名称数量为 {ModifyFileNameCount}个 \n\r修改的目录名称数量为 {ModifyDirectoryCount}个 \n\r修改的文件内容的数量为 {ModifyFileContentCount}个 "
                    : "未找到符合条件的文件和目录";
                MessageBox.Show(msg);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"重命名失败，异常:{ex.Message}");
            }
        }

        private void InitCount()
        {
            ModifyFileContentCount = 0;
            ModifyFileNameCount = 0;
            ModifyDirectoryCount = 0;
        }


        /// <summary>
        /// 根据文件获取目录(不包含文件名的)
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        private string GetDirectoryPathByFile(string filePath)
        {
            return filePath.Substring(0, filePath.LastIndexOf("\\"));
        }

        /// <summary>
        /// 目录和文件名称重命名
        /// </summary>
        /// <param name="targetInfo"></param>
        /// <param name="oldName"></param>
        /// <param name="newName"></param>
        public void RenameFileAndDirectoriesNames(DirectoryInfo targetInfo, string oldName, string newName)
        {
            #region 修改当前目录目录和文件

            //循环遍历子目录做递归操作
            LoopModifyChildrenDirecoties(targetInfo.GetDirectories(), oldName, newName);

            //修改文件内容+名称
            foreach (var file in targetInfo.GetFiles())
            {
                //1、修改文件内容包含需要替换的名字
                RenameFileContent(file, oldName, newName);

                //2、修改文件名包含需要替换的名字的
                if (file.Name.Contains(oldName))
                {
                    RenameFileName(file, oldName, newName);
                }
            }

            //3.修改目录名称
            foreach (var directory in targetInfo.GetDirectories())
            {
                //特定情况下，虽然目录里面文件重命名方法MoveTo返回了，但是底层的OS机制仍可能正在对该文件重命名，还未完成。
                //这时如果在重命名改目录，会被报"拒绝访问路径",所以下面每次重命名目录时休眠一会
                Thread.Sleep(100);

                RenameDirectory(directory, oldName, newName);
            }

            #endregion
        }


        /// <summary>
        /// 1、重命名文件内容包含旧项目名称的
        /// </summary>
        /// <param name="file"></param>
        /// <param name="oldName"></param>
        /// <param name="newName"></param>
        private void RenameFileContent(FileInfo file, string oldName, string newName)
        {
            var content = File.ReadAllText(file.FullName);
            if (!content.Contains(oldName))
            {
                return;
            }

            content = content.Replace(oldName, newName);
            using (var sw = new StreamWriter(file.FullName))
            {
                sw.Write(content);
                sw.Close();
            }

            ModifyFileContentCount++;
        }

        /// <summary>
        /// 2、重命名文件名包含旧项目名称的
        /// </summary>
        /// <param name="file"></param>
        /// <param name="oldName"></param>
        /// <param name="newName"></param>
        private void RenameFileName(FileInfo file, string oldName, string newName)
        {
            if (!file.Name.Contains(oldName))
            {
                return;
            }

            var newFileName = file.Name.Replace(oldName, newName);
            var newFileFullPath = System.IO.Path.Combine(GetDirectoryPathByFile(file.FullName), newFileName);
            file.MoveTo(newFileFullPath, true);
            ModifyFileNameCount++;
        }

        /// <summary>
        /// 3、重命名目录包含旧项目名称的
        /// </summary>
        /// <param name="directory"></param>
        /// <param name="oldName"></param>
        /// <param name="newName"></param>
        private void RenameDirectory(DirectoryInfo directory, string oldName, string newName)
        {
            var directoryName = directory.Name;
            if (!directoryName.Contains(oldName))
            {
                return;
            }

            var newDirectoryFullPath = System.IO.Path.Combine(GetDirectoryPathByFile(directory.FullName),
                directoryName.Replace(oldName, newName));
            directory.MoveTo(newDirectoryFullPath);
            ModifyDirectoryCount++;
        }


        /// <summary>
        /// 循环遍历子目录
        /// </summary>
        /// <param name="targetInfo">DirectoryInfo</param>
        /// <param name="oldName">旧名字</param>
        /// <param name="newName">新名字</param>
        private void LoopModifyChildrenDirecoties(DirectoryInfo[] targetInfo, string oldName, string newName)
        {
            //递归，遍历子目录
            foreach (var item in targetInfo)
            {
                if (IgnoreDirectories.Split(",").Contains(item.FullName.Substring(item.FullName.LastIndexOf("\\") + 1)))
                {
                    continue;
                }
                RenameFileAndDirectoriesNames(item, oldName, newName);
            }
        }


    }
}
