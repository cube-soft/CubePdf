﻿/* ------------------------------------------------------------------------- */
/*
 *  Converter.cs
 *
 *  Copyright (c) 2009 - 2011 CubeSoft, Inc. All rights reserved.
 *
 *  This program is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  This program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with this program.  If not, see < http://www.gnu.org/licenses/ >.
 */
/* ------------------------------------------------------------------------- */
using System;
using System.IO;
using System.Collections.Generic;

namespace CubePDF {
    /* --------------------------------------------------------------------- */
    /// Converter
    /* --------------------------------------------------------------------- */
    public class Converter {
        /* ----------------------------------------------------------------- */
        ///
        /// Run
        /// 
        /// NOTE: 文書プロパティ，パスワード関連とファイル結合は iText
        /// に任せる．出力パスに指定されたファイルが存在する場合がある．
        /// そのときは，_setting.ExistedFile の指定に従う．
        /// ExistedFile: 上書き，先頭に結合，末尾に結合
        /// 結合部分は，iText が行う．
        /// 
        /* ----------------------------------------------------------------- */
        public bool Run(UserSetting setting) {
            // Ghostscript に指定するパスに日本語が入るとエラーが発生する
            // 場合があるので，作業ディレクトリを変更する．
            this.CreateWorkingDirectory(setting);

            Ghostscript.Converter gs = new Ghostscript.Converter(Parameter.Device(setting.FileType, setting.Grayscale));
            bool status = true;
            try {
                gs.AddInclude(setting.LibPath + @"\lib");
                gs.AddSource(setting.InputPath);
                gs.PageRotation = setting.PageRotation;
                gs.Resolution = Parameter.ResolutionValue(setting.Resolution);
                gs.Destination = setting.OutputPath;

                this.ConfigDownSampling(setting, gs);
                if (Parameter.IsImageType(setting.FileType)) this.ConfigImage(setting, gs);
                else this.ConfigDocument(setting, gs);

                // NOTE: マージオプションが有効なのは PDF のみ．
                if (setting.FileType == Parameter.FileTypes.PDF) this.EscapeExistedFile(setting);

                gs.Run();

                if (setting.FileType == Parameter.FileTypes.PDF) {
                    PDFModifier modifier = new PDFModifier(_escaped);
                    status &= modifier.Run(setting);
                    if (!status && modifier.Messages.Count > 0) _messages.AddRange(modifier.Messages);
                }
                
                PostProcess postproc = new PostProcess();
                status &= postproc.Run(setting);
                if (!status && postproc.Messages.Count > 0) _messages.AddRange(postproc.Messages);
            }
            catch (Exception err) {
                if (gs.Messages.Count > 0) _messages.AddRange(gs.Messages);
                _messages.Add(new Message(Message.Levels.Error, err.Message));
                status = false;
            }
            finally {
                if (Directory.Exists(Utility.WorkingDirectory)) Directory.Delete(Utility.WorkingDirectory, true);
            }

            return status;
        }

        /* ----------------------------------------------------------------- */
        ///
        /// EscapeExistedFile
        ///
        /// <summary>
        /// マージオプションなどの関係で既に存在する同名ファイルを退避
        /// させる．
        /// </summary>
        ///
        /* ----------------------------------------------------------------- */
        public void EscapeExistedFile(UserSetting setting) {
            bool merge = (setting.ExistedFile == Parameter.ExistedFiles.MergeTail || setting.ExistedFile == Parameter.ExistedFiles.MergeHead);
            if (File.Exists(setting.OutputPath) && merge) {
                _escaped = Utility.WorkingDirectory + '\\' + Path.GetRandomFileName();
                File.Copy(setting.OutputPath, _escaped, true);
            }
        }

        /* ----------------------------------------------------------------- */
        /// CreateWorkingDirectory
        /* ----------------------------------------------------------------- */
        public void CreateWorkingDirectory(UserSetting setting) {
            Utility.WorkingDirectory = setting.LibPath + '\\' + Path.GetRandomFileName();
            if (File.Exists(Utility.WorkingDirectory)) File.Delete(Utility.WorkingDirectory);
            if (Directory.Exists(Utility.WorkingDirectory)) Directory.Delete(Utility.WorkingDirectory, true);
            Directory.CreateDirectory(Utility.WorkingDirectory);
        }

        /* ----------------------------------------------------------------- */
        /// Messages
        /* ----------------------------------------------------------------- */
        public List<CubePDF.Message> Messages {
            get { return _messages; }
        }

        /* ----------------------------------------------------------------- */
        //  UserSetting の値を基に各種設定を行う
        /* ----------------------------------------------------------------- */
        #region Configuration

        /* ----------------------------------------------------------------- */
        ///
        /// ConfigImage
        ///
        /// <summary>
        /// bmp, png, jpeg, gif のビットマップ系ファイルに変換するために
        /// 必要なオプションを設定する．
        /// </summary>
        ///
        /* ----------------------------------------------------------------- */
        public void ConfigImage(UserSetting setting, Ghostscript.Converter gs) {
            gs.AddOption("GraphicsAlphaBits", 4);
            gs.AddOption("TextAlphaBits", 4);
        }

        /* ----------------------------------------------------------------- */
        ///
        /// ConfigDocument
        ///
        /// <summary>
        /// pdf, ps, eps, svg のベクター系ファイルに変換するために必要な
        /// オプションを設定する．
        /// </summary>
        ///
        /* ----------------------------------------------------------------- */
        public void ConfigDocument(UserSetting setting, Ghostscript.Converter gs) {
            if (setting.EmbedFont) {
                gs.AddOption("EmbedAllFonts", "true");
                gs.AddOption("SubsetFonts", "true");
            }

            if (setting.FileType == Parameter.FileTypes.PDF) this.ConfigPDF(setting, gs);
        }

        /* ----------------------------------------------------------------- */
        /// ConfigPDF
        /* ----------------------------------------------------------------- */
        public void ConfigPDF(UserSetting setting, Ghostscript.Converter gs) {
            gs.AddOption("CompatibilityLevel", Parameter.PDFVersionValue(setting.PDFVersion));
            gs.AddOption("UseFlateCompression", "false");
            
            if (setting.Grayscale) {
                gs.AddOption("ProcessColorModel", "/DeviceGray");
                gs.AddOption("ColorConversionStrategy", "/Gray");
            }
        }

        /* ----------------------------------------------------------------- */
        /// ConfigDownSampling
        /* ----------------------------------------------------------------- */
        public void ConfigDownSampling(UserSetting setting, Ghostscript.Converter gs) {
            gs.AddOption("ColorImageResolution", Parameter.ResolutionValue(setting.Resolution));
            gs.AddOption("GrayImageResolution", Parameter.ResolutionValue(setting.Resolution));
            gs.AddOption("MonoImageResolution", 300);

            if (setting.DownSampling == Parameter.DownSamplings.None) {
                gs.AddOption("DownsampleColorImages", false);
                gs.AddOption("AutoFilterColorImages", false);
                gs.AddOption("DownsampleGrayImages", false);
                gs.AddOption("AutoFilterGrayImages", false);
                gs.AddOption("DownsampleMonoImages", false);
                gs.AddOption("MonoImageFilter", "/CCITTFaxEncode");
            }
            else if (setting.DownSampling == Parameter.DownSamplings.Average) {
                gs.AddOption("DownsampleColorImages", true);
                gs.AddOption("ColorImageDownsampleType", "/Average");
                gs.AddOption("AutoFilterColorImages", true);
                gs.AddOption("DownsampleGrayImages", true);
                gs.AddOption("GrayImageDownsampleType", "/Average");
                gs.AddOption("AutoFilterGrayImages", true);
                gs.AddOption("DownsampleMonoImages", true);
                gs.AddOption("MonoImageDownsampleType", "/Average");
                gs.AddOption("MonoImageFilter", "/CCITTFaxEncode");
            }
            else if (setting.DownSampling == Parameter.DownSamplings.Bicubic) {
                gs.AddOption("DownsampleColorImages", true);
                gs.AddOption("ColorImageDownsampleType", "/Bicubic");
                gs.AddOption("AutoFilterColorImages", true);
                gs.AddOption("DownsampleGrayImages", true);
                gs.AddOption("GrayImageDownsampleType", "/Bicubic");
                gs.AddOption("AutoFilterGrayImages", true);
                gs.AddOption("DownsampleMonoImages", true);
                gs.AddOption("MonoImageDownsampleType", "/Bicubic");
                gs.AddOption("MonoImageFilter", "/CCITTFaxEncode");
            }
            else if (setting.DownSampling == Parameter.DownSamplings.Subsample) {
                gs.AddOption("DownsampleColorImages", true);
                gs.AddOption("ColorImageDownsampleType", "/Subsample");
                gs.AddOption("AutoFilterColorImages", true);
                gs.AddOption("DownsampleGrayImages", true);
                gs.AddOption("GrayImageDownsampleType", "/Subsample");
                gs.AddOption("AutoFilterGrayImages", true);
                gs.AddOption("DownsampleMonoImages", true);
                gs.AddOption("MonoImageDownsampleType", "/Subsample");
                gs.AddOption("MonoImageFilter", "/CCITTFaxEncode");
            }
        }

        #endregion

        /* ----------------------------------------------------------------- */
        //  変数の定義
        /* ----------------------------------------------------------------- */
        #region Variables
        private string _escaped = null; // null 以外ならマージが必要
        private List<CubePDF.Message> _messages = new List<Message>();
        #endregion
    }
}
