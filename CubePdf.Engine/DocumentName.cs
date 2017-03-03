﻿/* ------------------------------------------------------------------------- */
///
/// DocumentName.cs
///
/// Copyright (c) 2009 CubeSoft, Inc. All rights reserved.
///
/// This program is free software: you can redistribute it and/or modify
/// it under the terms of the GNU Affero General Public License as published
/// by the Free Software Foundation, either version 3 of the License, or
/// (at your option) any later version.
///
/// This program is distributed in the hope that it will be useful,
/// but WITHOUT ANY WARRANTY; without even the implied warranty of
/// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
/// GNU Affero General Public License for more details.
///
/// You should have received a copy of the GNU Affero General Public License
/// along with this program.  If not, see <http://www.gnu.org/licenses/>.
///
/* ------------------------------------------------------------------------- */
using System;
using IoEx = System.IO;

namespace CubePdf
{
    /* --------------------------------------------------------------------- */
    ///
    /// DocumentName
    ///
    /// <summary>
    /// プリンタの文書名からファイル名として問題ない文字列へ変換するための
    /// クラスです。
    /// </summary>
    ///
    /* --------------------------------------------------------------------- */
    public abstract class DocumentName
    {
        #region Public methods

        /* ----------------------------------------------------------------- */
        ///
        /// GetFileName
        ///
        /// <summary>
        /// DocumentName を利用してファイル名を生成します。
        /// </summary>
        /// 
        /// <remarks>
        /// DocumentName は、以下のパターンに分かれます。
        /// 
        /// 1. ファイル名のみ
        /// 2. アプリケーション名 - ファイル名
        /// 3. ファイル名 - アプリケーション名
        /// 
        /// これらのパターンを想定して、拡張子と思われる文字列を基にして
        /// ファイル名部分を判別します。拡張子がどこにも存在しない場合は、
        /// DocumentName 自身を返す事とします。
        /// </remarks>
        ///
        /* ----------------------------------------------------------------- */
        public static string CreateFileName(string src)
        {
            var default_value = Properties.Resources.ProductName;

            try
            {
                if (string.IsNullOrEmpty(src)) return default_value;
                var docname = ModifyFilename(src);
                if (string.IsNullOrEmpty(docname)) return default_value;

                var search = " - ";
                var pos = docname.LastIndexOf(search);
                if (pos == -1) return docname;
                else if (IoEx.Path.HasExtension(docname.Substring(0, pos)))
                {
                    return docname.Substring(0, pos);
                }
                else if (IoEx.Path.HasExtension(docname.Substring(pos, docname.Length - pos)))
                {
                    pos = docname.IndexOf(search);
                    System.Diagnostics.Debug.Assert(pos != -1);
                    pos += search.Length;
                    return docname.Substring(pos, docname.Length - pos);
                }
                else return docname;
            }
            catch (Exception err)
            {
                Cube.Log.Operations.Warn(typeof(DocumentName), err.Message, err);
                return default_value;
            }
        }

        #endregion

        #region Other methods

        /* ----------------------------------------------------------------- */
        ///
        /// ModifyFileName
        /// 
        /// <summary>
        /// ファイル名として不正な文字を '_' (アンダースコア) に置換します。
        /// </summary>
        /// 
        /* ----------------------------------------------------------------- */
        private static string ModifyFilename(string filename) {
            var normalize = CubePdf.Misc.Path.Normalize(filename, '_');
            var dest = IoEx.Path.GetFileName(normalize);
            if (dest.ToLower() == "pptview") {
                var s = FindFromRecent(".ppt");
                if (s == null) s = FindFromRecent(".pptx");
                if (s != null) dest = s;
            }

            Cube.Log.Operations.Debug(typeof(DocumentName),
                string.Format("{0}{1}\t-> {2}{3}\t-> {4}",
                filename,  Environment.NewLine,
                normalize, Environment.NewLine,
                dest
            ));
            return dest;
        }
        
        /* ----------------------------------------------------------------- */
        ///
        /// FindFromRecent
        ///
        /// <summary>
        /// 「最近使ったファイル一覧」から、引数に指定された拡張子のファイル
        /// の内、直近に使用したファイル名を返します。
        /// </summary>
        ///
        /* ----------------------------------------------------------------- */
        private static string FindFromRecent(string ext) {
            var dir = System.Environment.GetFolderPath(Environment.SpecialFolder.Recent);
            var info = new IoEx.DirectoryInfo(dir);
            string dest = null;

            foreach (var file in info.GetFiles()) {
                System.String filename = IoEx.Path.GetFileNameWithoutExtension(file.FullName);
                System.String s = IoEx.Path.GetExtension(filename).ToLower();
                if (s == ext.ToLower()) {
                    if (dest == null) dest = file.FullName;
                    else {
                        System.DateTime prev = IoEx.File.GetLastWriteTime(dest);
                        System.DateTime cur = IoEx.File.GetLastWriteTime(file.FullName);
                        if (cur.CompareTo(prev) >= 0) dest = file.FullName;
                    }
                }
            }
            return (dest == null) ? null : IoEx.Path.GetFileNameWithoutExtension(dest);
        }

        #endregion
    }
}
