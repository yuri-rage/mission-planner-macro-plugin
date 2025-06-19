using MissionPlanner.Plugin;
using MissionPlanner.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using MissionPlanner.Controls;
using System.Linq;

namespace MissionMacro
{
    public class MissionMacro : Plugin
    {
        public override string Name => "Mission Macro";
        public override string Version => "v0.1.0-alpha";
        public override string Author => "Yuri Rage";

        private MyDataGridView commands;

        //[DebuggerHidden]
        public override bool Init() => true;
        public override bool Exit() => true;

        public override bool Loaded()
        {
            commands = Host.MainForm.FlightPlanner.Commands;

            var contextMenuItem = new ToolStripMenuItem("Add Macro");
            contextMenuItem.Click += (sender, e) =>
            {
                AddMacro(sender, e);
            };
            int insertIndex = Math.Min(3, Host.FPMenuMap.Items.Count); // just in case context menu shrinks someday
            Host.FPMenuMap.Items.Insert(insertIndex, contextMenuItem);

            return true;
        }

        private void AddMacro(object sender, EventArgs e)
        {
            var macroCommands = GetMacroCommands(out string macroFilename);
            if (macroCommands.Count < 2) { return; } // silently return on file failure or user cancel

            var result = CreateCommandGrid();

            int commandIndex = 0;

            // combine macro commands and current commands
            foreach (Locationwp cmd in macroCommands)
            {
                if (cmd.frame == 0) { continue; } // skip home

                var commandName = Enum.GetValues(typeof(MAVLink.MAV_CMD))
                    .Cast<object>()
                    .FirstOrDefault(v => (ushort)v == cmd.id)?.ToString() ?? "UNKNOWN";

                // special case for DO_JUMP with populated param 4
                if (commandName == "DO_JUMP" && cmd.p4 > 0)
                {
                    for (int i = commandIndex; i < Math.Min(cmd.p4, commands.Rows.Count); i++)
                    {
                        result.Rows.Add(DeepCLoneRow(commands.Rows[i]));
                        commandIndex++;
                    }
                    continue;
                }

                var selectedRow = result.Rows.Add();
                if (result.Rows[selectedRow].Cells[0] is DataGridViewComboBoxCell cellcmd)
                {
                    cellcmd.Value = commandName;
                }
                var parameters = new[] { cmd.p1, cmd.p2, cmd.p3, cmd.p4, cmd.lat, cmd.lng, cmd.alt };
                for (int i = 0; i < parameters.Length; i++)
                {
                    result.Rows[selectedRow].Cells[i + 1].Value = parameters[i].ToString();
                }
            }

            commands.Rows.Clear();

            foreach (DataGridViewRow row in result.Rows)
            {
                commands.Rows.Add(DeepCLoneRow(row));
            }

            Host.MainForm.FlightPlanner.writeKML();
            Host.MainForm.FlightPlanner.lbl_wpfile.Text = $"Processed {Path.GetFileName(macroFilename)}";
        }

        private List<Locationwp> GetMacroCommands(out string filename)
        {
            using (OpenFileDialog fd = new OpenFileDialog())
            {
                fd.Filter = "All Supported Types|*.txt;*.waypoints;";
                if (Directory.Exists(Settings.Instance["WPFileDirectory"] ?? ""))
                    fd.InitialDirectory = Settings.Instance["WPFileDirectory"];
                fd.ShowDialog();
                filename = fd.FileName;

                if (File.Exists(filename))
                {
                    Settings.Instance["WPFileDirectory"] = Path.GetDirectoryName(filename);
                    return WaypointFile.ReadWaypointFile(filename);
                }
                return new List<Locationwp>(); // return empty list on cancel or failure
            }
        }

        private MyDataGridView CreateCommandGrid()
        {
            var newGrid = new MyDataGridView
            {
                AllowUserToAddRows = false // prevents empty row from being added by default
            };
            foreach (DataGridViewColumn col in commands.Columns)
            {
                var newCol = col.Clone() as DataGridViewColumn;
                newGrid.Columns.Add(newCol);
            }
            return newGrid;
        }

        public static DataGridViewRow DeepCLoneRow(DataGridViewRow row)
        {
            var clonedRow = row.Clone() as DataGridViewRow;
            for (int i = 0; i < row.Cells.Count; i++)
            {
                clonedRow.Cells[i].Value = row.Cells[i].Value;
            }
            return clonedRow;
        }
    }
}