﻿using DSCPullServerWeb.Databases;
using DSCPullServerWeb.Models;
using DSCPullServerWeb.Helpers;
using Microsoft.Isam.Esent.Interop;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using System.Text;

namespace DSCPullServerWeb.Services {
    public class EsentDatabaseRepository : IDatabaseRepository {
        private const string DATABASE_NAME = "Devices.edb";

        private const string TABLE_DEVICES = "Devices";

        private const string TABLE_REGISTRATION_DATA = "RegistrationData";

        private const string TABLE_STATUS_REPORT = "StatusReport";

        private ILogger _logger;

        private IOptions _options;

        private EsentDatabase _database;

        public EsentDatabaseRepository(ILogger logger, IOptions options) {
            _logger = logger;
            _options = options;

            _database = new EsentDatabase(_options.Name, Path.Combine(_options.DatabasePath, DATABASE_NAME));
            _database.Open();
        }

        #region Id Nodes Interface

        public IList<IdNode> GetIdNodes() {
            IList<IdNode> nodes = new List<IdNode>();

            using (EsentSession session = new EsentSession(_database)) {
                session.Open();

                JET_SESID sessionId = session.GetSessionId();
                JET_DBID databaseId = session.GetDatabaseId();
                JET_TABLEID tableId;

                if (DatabaseTableExists(sessionId, databaseId, TABLE_DEVICES)) {
                    Api.OpenTable(sessionId, databaseId, TABLE_DEVICES, OpenTableGrbit.ReadOnly, out tableId);

                    Api.MoveBeforeFirst(session.GetSessionId(), tableId);

                    while (Api.TryMoveNext(sessionId, tableId)) {
                        IDictionary<string, JET_COLUMNID> columnDictionary = Api.GetColumnDictionary(sessionId, tableId);

                        IdNode node = new IdNode() {
                            ConfigurationID = Api.RetrieveColumnAsString(sessionId, tableId, columnDictionary["ConfigurationID"]),
                            TargetName = Api.RetrieveColumnAsString(sessionId, tableId, columnDictionary["TargetName"]),
                            NodeCompliant = Api.RetrieveColumnAsBoolean(sessionId, tableId, columnDictionary["NodeCompliant"]).GetValueOrDefault(),
                            Dirty = Api.RetrieveColumnAsBoolean(sessionId, tableId, columnDictionary["Dirty"]).GetValueOrDefault(),
                            LastHeartbeatTime = Api.RetrieveColumnAsDateTime(sessionId, tableId, columnDictionary["LastHeartbeatTime"]).GetValueOrDefault(),
                            LastComplianceTime = Api.RetrieveColumnAsDateTime(sessionId, tableId, columnDictionary["LastComplianceTime"]).GetValueOrDefault(),
                            StatusCode = Api.RetrieveColumnAsInt32(sessionId, tableId, columnDictionary["StatusCode"]).GetValueOrDefault(),
                            ServerCheckSum = Api.RetrieveColumnAsString(sessionId, tableId, columnDictionary["ServerCheckSum"]),
                            TargetCheckSum = Api.RetrieveColumnAsString(sessionId, tableId, columnDictionary["TargetCheckSum"])
                        };

                        nodes.Add(node);
                    }

                    Api.JetCloseTable(sessionId, tableId);
                } else {
                    _logger.Log(10091, String.Format("Database table {0} not found!", TABLE_DEVICES), LogLevel.Warning);
                }
            }

            return nodes;
        }

        #endregion

        #region Names Nodes Interface

        public IList<NamesNode> GetNamesNodes() {
            IList<NamesNode> nodes = new List<NamesNode>();

            using (EsentSession session = new EsentSession(_database)) {
                session.Open();

                JET_SESID sessionId = session.GetSessionId();
                JET_DBID databaseId = session.GetDatabaseId();
                JET_TABLEID tableId;

                if (DatabaseTableExists(sessionId, databaseId, TABLE_REGISTRATION_DATA)) {
                    Api.OpenTable(sessionId, databaseId, TABLE_REGISTRATION_DATA, OpenTableGrbit.ReadOnly, out tableId);

                    Api.MoveBeforeFirst(session.GetSessionId(), tableId);

                    while (Api.TryMoveNext(sessionId, tableId)) {
                        IDictionary<string, JET_COLUMNID> columnDictionary = Api.GetColumnDictionary(sessionId, tableId);

                        NamesNode node = new NamesNode() {
                            AgentId = Api.RetrieveColumnAsString(sessionId, tableId, columnDictionary["AgentId"]),
                            NodeName = Api.RetrieveColumnAsString(sessionId, tableId, columnDictionary["NodeName"]),
                            LCMVersion = Api.RetrieveColumnAsString(sessionId, tableId, columnDictionary["LCMVersion"]),
                            IPAddress = Api.RetrieveColumnAsString(sessionId, tableId, columnDictionary["IPAddress"]),
                            ConfigurationNames = ((List<string>)Api.DeserializeObjectFromColumn(sessionId, tableId, columnDictionary["ConfigurationNames"]))
                        };

                        nodes.Add(node);
                    }

                    Api.JetCloseTable(sessionId, tableId);
                } else {
                    _logger.Log(10091, String.Format("Database table {0} not found!", TABLE_REGISTRATION_DATA), LogLevel.Warning);
                }
            }

            return nodes;
        }

        #endregion

        #region Report Interface

        public IList<Report> GetReports() {
            List<Report> reports = new List<Report>();

            using (EsentSession session = new EsentSession(_database)) {
                session.Open();

                JET_SESID sessionId = session.GetSessionId();
                JET_DBID databaseId = session.GetDatabaseId();
                JET_TABLEID tableId;

                if (DatabaseTableExists(sessionId, databaseId, TABLE_STATUS_REPORT)) {
                    IList<string> nodeNames = GetNamesNodes().Select(node => node.NodeName).ToList();

                    Api.OpenTable(sessionId, databaseId, TABLE_STATUS_REPORT, OpenTableGrbit.ReadOnly, out tableId);

                    Api.MoveBeforeFirst(session.GetSessionId(), tableId);


                    while (Api.TryMoveNext(sessionId, tableId)) {
                        IDictionary<string, JET_COLUMNID> columnDictionary = Api.GetColumnDictionary(sessionId, tableId);

                        List<string> statusData = (List<string>)Api.DeserializeObjectFromColumn(sessionId, tableId, columnDictionary["StatusData"]);

                        if (statusData.Count != 0) {
                            string NodeName = Api.RetrieveColumnAsString(sessionId, tableId, columnDictionary["NodeName"]);

                            if(string.IsNullOrEmpty(NodeName)) {
                                //No node name present - this record won't be displayed.
                                continue;
                            }

                            string NotCompliantResources = string.Empty;

                            StatusDataElement dataElement = JsonConvert.DeserializeObject<StatusDataElement>(statusData[0]);

                            List<Report> existingReport = reports.Where(r => r.NodeName == NodeName).ToList();

                            if (existingReport.Count > 1) {
                                throw new Exception("There are multiple servers with the same name in Reports array!");
                            }
                            if (existingReport.Count == 1) {
                                if (existingReport[0].StartTime < dataElement.StartDate) {
                                    //A newer record is found - the existing one should be deleted.                                 
                                    reports.Remove(existingReport[0]);
                                } else {
                                    //Go to next record.
                                    continue;
                                }
                            }

                            if (dataElement.ResourcesNotInDesiredState?.Count != null) {

                                StringBuilder sb = new StringBuilder();

                                foreach (ResourceState resource in dataElement.ResourcesNotInDesiredState) {
                                    if (sb.Length != 0) {
                                        sb.Append(';');
                                        sb.Append(resource.ResourceId);
                                    } else {
                                        sb.Append(resource.ResourceId);
                                    }
                                }
                                NotCompliantResources = sb.ToString();
                            }

                            Report report = new Report() {
                                Id = Api.RetrieveColumnAsString(sessionId, tableId, columnDictionary["Id"]),
                                JobId = Api.RetrieveColumnAsString(sessionId, tableId, columnDictionary["JobId"]),
                                NodeName = NodeName,
                                IPAddress = Api.RetrieveColumnAsString(sessionId, tableId, columnDictionary["IPAddress"]),
                                RerfreshMode = Api.RetrieveColumnAsString(sessionId, tableId, columnDictionary["RefreshMode"]),
                                OperationType = Api.RetrieveColumnAsString(sessionId, tableId, columnDictionary["OperationType"]),
                                Status = Api.RetrieveColumnAsString(sessionId, tableId, columnDictionary["Status"]),
                                RebootRequested = Api.RetrieveColumnAsBoolean(sessionId, tableId, columnDictionary["RebootRequested"]).GetValueOrDefault(),
                                StartTime = Api.RetrieveColumnAsDateTime(sessionId, tableId, columnDictionary["StartTime"]).GetValueOrDefault(),
                                EndTime = Api.RetrieveColumnAsDateTime(sessionId, tableId, columnDictionary["EndTime"]).GetValueOrDefault(),
                                LastModifiedTime = Api.RetrieveColumnAsDateTime(sessionId, tableId, columnDictionary["LastModifiedTime"]).GetValueOrDefault(),
                                LCMVersion = Api.RetrieveColumnAsString(sessionId, tableId, columnDictionary["LCMVersion"]),
                                ConfigurationVersion = Api.RetrieveColumnAsString(sessionId, tableId, columnDictionary["ConfigurationVersion"]),
                                ReportFormatVersion = Api.RetrieveColumnAsString(sessionId, tableId, columnDictionary["ReportFormatVersion"]),
                                Errors = (List<string>)Api.DeserializeObjectFromColumn(sessionId, tableId, columnDictionary["Errors"]),
                                StatusData = statusData[0],
                                NotCompliantResources = NotCompliantResources
                            };

                            // Field AdditionalData is only available on WS2016 and WMF 5.1
                            if (columnDictionary.Keys.Contains("AdditionalData")) {
                                report.AdditionalData = Api.RetrieveColumnAsString(sessionId, tableId, columnDictionary["AdditionalData"]);
                            }

                            reports.Add(report);
                        }
                    }

                    Api.JetCloseTable(sessionId, tableId);
                } else {
                    _logger.Log(10091, String.Format("Database table {0} not found!", TABLE_STATUS_REPORT), LogLevel.Warning);
                }
            }

            return reports.OrderBy(r => r.StartTime).ToList();
        }

        #endregion

        public static Boolean DatabaseExist(String path) {
            return File.Exists(Path.Combine(path, DATABASE_NAME));
        }

        private Boolean DatabaseTableExists(JET_SESID sessionId, JET_DBID databaseId, String tableName) {
            IEnumerable<String> tableNames = Api.GetTableNames(sessionId, databaseId);

            return tableNames.Contains(tableName);
        }
    }
}
