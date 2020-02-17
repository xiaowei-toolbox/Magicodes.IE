﻿// ======================================================================
// 
//           filename : ExcelExporter.cs
//           description :
// 
//           created by 雪雁 at  2019-09-11 13:51
//           文档官网：https://docs.xin-lai.com
//           公众号教程：麦扣聊技术
//           QQ群：85318032（编程交流）
//           Blog：http://www.cnblogs.com/codelove/
// 
// ======================================================================

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Magicodes.ExporterAndImporter.Core;
using Magicodes.ExporterAndImporter.Core.Extension;
using Magicodes.ExporterAndImporter.Core.Filters;
using Magicodes.ExporterAndImporter.Core.Models;
using Magicodes.ExporterAndImporter.Excel.Utility;
using Magicodes.ExporterAndImporter.Excel.Utility.TemplateExport;
using OfficeOpenXml;
using OfficeOpenXml.Table;

namespace Magicodes.ExporterAndImporter.Excel
{
    /// <summary>
    ///     Excel导出程序
    /// </summary>
    public class ExcelExporter : IExporter, IExportFileByTemplate
    {

        /// <summary>
        ///     导出Excel
        /// </summary>
        /// <param name="fileName">文件名</param>
        /// <param name="dataItems">数据列</param>
        /// <param name="exportType">导出类型</param>
        /// <returns>文件</returns>
        public async Task<ExportFileInfo> Export<T>(string fileName, ICollection<T> dataItems, EnumExportType exportType = EnumExportType.Xls) where T : class
        {
            fileName.CheckExcelFileName();
            var
            bytes=
                 await ExportAsByteArray(dataItems,exportType);
            return bytes.ToExcelExportFileInfo(fileName);
        }


        /// <summary>
        ///     导出Excel
        /// </summary>
        /// <param name="dataItems">数据</param>
        /// <param name="exportType">导出类型</param>
        /// <returns>文件二进制数组</returns>
        public Task<byte[]> ExportAsByteArray<T>(ICollection<T> dataItems, EnumExportType exportType = EnumExportType.Xls) where T : class
        {
            var helper = new ExportHelper<T>();
            if (helper.ExcelExporterSettings.MaxRowNumberOnASheet > 0 && dataItems.Count > helper.ExcelExporterSettings.MaxRowNumberOnASheet)
            {
                using (helper.CurrentExcelPackage)
                {
                    var sheetCount = (int)(dataItems.Count / helper.ExcelExporterSettings.MaxRowNumberOnASheet) + ((dataItems.Count % helper.ExcelExporterSettings.MaxRowNumberOnASheet) > 0 ? 1 : 0);
                    for (int i = 0; i < sheetCount; i++)
                    {
                        var sheetDataItems = dataItems.Skip(i * helper.ExcelExporterSettings.MaxRowNumberOnASheet).Take(helper.ExcelExporterSettings.MaxRowNumberOnASheet).ToList();
                        helper.AddExcelWorksheet();
                        helper.Export(sheetDataItems);
                    }
                    //TODO Csv多sheet
                    return Task.FromResult(helper.CurrentExcelPackage.GetAsByteArray());
                }
            }
            else
            {
                using (var ep = helper.Export(dataItems))
                {
                    return Task.FromResult(exportType == EnumExportType.Csv ? helper.GetCsvExportAsByteArray(dataItems) : ep.GetAsByteArray());
                }
            }

        }

        /// <summary>
        /// 导出DataTable
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="fileName"></param>
        /// <param name="dataItems"></param>
        /// <returns></returns>
        public async Task<ExportFileInfo> Export<T>(string fileName, DataTable dataItems) where T : class
        {
            fileName.CheckExcelFileName();
            var bytes = await ExportAsByteArray<T>(dataItems);
            return bytes.ToExcelExportFileInfo(fileName);
        }

        /// <summary>
        /// 导出字节
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="dataItems"></param>
        /// <returns></returns>
        public Task<byte[]> ExportAsByteArray<T>(DataTable dataItems) where T : class
        {
            var helper = new ExportHelper<T>();
            if (helper.ExcelExporterSettings.MaxRowNumberOnASheet > 0 && dataItems.Rows.Count > helper.ExcelExporterSettings.MaxRowNumberOnASheet)
            {
                using (helper.CurrentExcelPackage)
                {
                    var ds = dataItems.SplitDataTable(helper.ExcelExporterSettings.MaxRowNumberOnASheet);
                    var sheetCount = ds.Tables.Count;
                    for (int i = 0; i < sheetCount; i++)
                    {
                        var sheetDataItems = ds.Tables[i];
                        helper.AddExcelWorksheet();
                        helper.Export(sheetDataItems);
                    }
                    return Task.FromResult(helper.CurrentExcelPackage.GetAsByteArray());
                }
            }
            else
            {
                using (var ep = helper.Export(dataItems))
                {
                    return Task.FromResult(ep.GetAsByteArray());
                }
            }
        }

        /// <summary>
        ///     导出excel表头
        /// </summary>
        /// <param name="items">表头数组</param>
        /// <param name="sheetName">工作簿名称</param>
        /// <returns></returns>
        public Task<byte[]> ExportHeaderAsByteArray(string[] items, string sheetName = "导出结果")
        {
            var helper = new ExportHelper<DataTable>();
            var headerList = new List<ExporterHeaderInfo>();
            for (var i = 1; i <= items.Length; i++)
            {
                var item = items[i - 1];
                var exporterHeaderInfo =
                     new ExporterHeaderInfo()
                     {
                         Index = i,
                         DisplayName = item,
                         CsTypeName = "string",
                         PropertyName = item,
                         ExporterHeaderAttribute = new ExporterHeaderAttribute(item) { }
                     };
                headerList.Add(exporterHeaderInfo);
            }
            helper.AddExcelWorksheet(sheetName);
            helper.AddExporterHeaderInfoList(headerList);
            using (var ep = helper.ExportHeaders())
            {
                return Task.FromResult(ep.GetAsByteArray());
            }
        }

        /// <summary>
        ///     导出Excel表头
        /// </summary>
        /// <param name="type">类型</param>
        /// <returns>文件二进制数组</returns>
        public Task<byte[]> ExportHeaderAsByteArray<T>(T type) where T : class
        {
            var helper = new ExportHelper<T>();
            using (var ep = helper.ExportHeaders())
            {
                return Task.FromResult(ep.GetAsByteArray());
            }
        }


        /// <summary>
        ///     根据模板导出
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="fileName"></param>
        /// <param name="data"></param>
        /// <param name="template">HTML模板或模板路径</param>
        /// <returns></returns>
        public Task<ExportFileInfo> ExportByTemplate<T>(string fileName, T data, string template) where T : class
        {
            using (var helper = new TemplateExportHelper<T>())
            {
                helper.Export(fileName, template, data);
                return Task.FromResult(new ExportFileInfo());
            }
        }

        public async Task<ExportFileInfo> Export(string fileName, DataTable dataItems, IExporterHeaderFilter exporterHeaderFilter = null, int maxRowNumberOnASheet = 1000000)
        {
            fileName.CheckExcelFileName();
            var bytes = await ExportAsByteArray(dataItems, exporterHeaderFilter, maxRowNumberOnASheet);
            return bytes.ToExcelExportFileInfo(fileName);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dataItems"></param>
        /// <param name="exporterHeaderFilter"></param>
        /// <returns></returns>
        public Task<byte[]> ExportAsByteArray(DataTable dataItems, IExporterHeaderFilter exporterHeaderFilter = null, int maxRowNumberOnASheet = 1000000)
        {
            var helper = new ExportHelper<DataTable>();
            helper.ExcelExporterSettings.MaxRowNumberOnASheet = maxRowNumberOnASheet;
            helper.SetExporterHeaderFilter(exporterHeaderFilter);

            if (helper.ExcelExporterSettings.MaxRowNumberOnASheet > 0 && dataItems.Rows.Count > helper.ExcelExporterSettings.MaxRowNumberOnASheet)
            {
                using (helper.CurrentExcelPackage)
                {
                    var ds = dataItems.SplitDataTable(helper.ExcelExporterSettings.MaxRowNumberOnASheet);
                    var sheetCount = ds.Tables.Count;
                    for (int i = 0; i < sheetCount; i++)
                    {
                        var sheetDataItems = ds.Tables[i];
                        helper.AddExcelWorksheet();
                        helper.Export(sheetDataItems);
                    }
                    return Task.FromResult(helper.CurrentExcelPackage.GetAsByteArray());
                }
            }
            else
            {
                using (var ep = helper.Export(dataItems))
                {
                    return Task.FromResult(ep.GetAsByteArray());
                }
            }
        }
    }
}