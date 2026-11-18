using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using razorpm.Data;
using razorpm.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace razorpm.Pages
{
    public class ReportsModel : BasePageModel
    {
        private readonly AppDbContext _db;

        public ReportsModel(AppDbContext db)
        {
            _db = db;
        }

        public List<ReportRow> EmployeeRows { get; private set; } = new();
        public List<ReportRow> ProjectRows { get; private set; } = new();
        public List<SelectListItem> ProjectOptions { get; private set; } = new();

        public string ReportTitle { get; private set; } = "Отчеты";
        public string ReportDescription { get; private set; } = "";

        public int ProjectsTotal { get; private set; }
        public int TasksTotal { get; private set; }
        public int TasksActive { get; private set; }
        public int TasksDone { get; private set; }
        public int TasksOverdue { get; private set; }
        public int TotalHours { get; private set; }

        [BindProperty(SupportsGet = true)]
        public int? projectId { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? archive { get; set; }

        public IActionResult OnGet()
        {
            var gate = RequireLogin();
            if (gate != null) return gate;

            if (!CanViewReports())
            {
                return RedirectToPage("/Index");
            }

            LoadReport();
            return Page();
        }

        public IActionResult OnPostDownloadEmployees(int? projectId, string? archive)
        {
            var gate = RequireLogin();
            if (gate != null) return gate;

            if (!CanViewReports())
            {
                return RedirectToPage("/Index");
            }

            this.projectId = projectId;
            this.archive = archive;

            LoadReport();

            var sb = new StringBuilder();
            sb.AppendLine("Сотрудник;Всего задач;Активные;Завершенные;Часы");

            foreach (var row in EmployeeRows)
            {
                sb.AppendLine($"{Csv(row.Employee)};{row.TotalTasks};{row.ActiveTasks};{row.DoneTasks};{row.TotalHours}");
            }

            var bytes = Encoding.UTF8.GetPreamble()
                .Concat(Encoding.UTF8.GetBytes(sb.ToString()))
                .ToArray();

            return File(bytes, "text/csv", "employees_report.csv");
        }

        public IActionResult OnPostDownloadProjects(int? projectId, string? archive)
        {
            var gate = RequireLogin();
            if (gate != null) return gate;

            if (!CanViewReports())
            {
                return RedirectToPage("/Index");
            }

            this.projectId = projectId;
            this.archive = archive;

            LoadReport();

            var sb = new StringBuilder();
            sb.AppendLine("Проект;Всего задач;Активные;Завершенные;Часы");

            foreach (var row in ProjectRows)
            {
                sb.AppendLine($"{Csv(row.Employee)};{row.TotalTasks};{row.ActiveTasks};{row.DoneTasks};{row.TotalHours}");
            }

            var bytes = Encoding.UTF8.GetPreamble()
                .Concat(Encoding.UTF8.GetBytes(sb.ToString()))
                .ToArray();

            return File(bytes, "text/csv", "projects_report.csv");
        }

        public IActionResult OnPostDownloadPdf(int? projectId, string? archive)
        {
            var gate = RequireLogin();
            if (gate != null) return gate;

            if (!CanViewReports())
            {
                return RedirectToPage("/Index");
            }

            this.projectId = projectId;
            this.archive = archive;

            LoadReport();

            var pdfBytes = GeneratePdfReport();

            return File(pdfBytes, "application/pdf", "tasks_report.pdf");
        }

        public IActionResult OnPostDownloadEmployeesPdf(int? projectId, string? archive)
        {
            var gate = RequireLogin();
            if (gate != null) return gate;

            if (!CanViewReports())
            {
                return RedirectToPage("/Index");
            }

            this.projectId = projectId;
            this.archive = archive;

            LoadReport();

            var pdfBytes = GenerateRowsPdfReport(
                "Отчет по сотрудникам",
                "Сотрудник",
                EmployeeRows);

            return File(pdfBytes, "application/pdf", "employees_report.pdf");
        }

        public IActionResult OnPostDownloadProjectsPdf(int? projectId, string? archive)
        {
            var gate = RequireLogin();
            if (gate != null) return gate;

            if (!CanViewReports())
            {
                return RedirectToPage("/Index");
            }

            this.projectId = projectId;
            this.archive = archive;

            LoadReport();

            var pdfBytes = GenerateRowsPdfReport(
                "Отчет по проектам",
                "Проект",
                ProjectRows);

            return File(pdfBytes, "application/pdf", "projects_report.pdf");
        }

        private bool CanViewReports()
        {
            return IsAdmin || IsDirector || IsManager;
        }

        private void LoadReport()
        {
            if (IsAdmin)
            {
                ReportTitle = "Аналитика системы";
                ReportDescription = "Общий отчет по проектам, задачам и сотрудникам";
            }
            else if (IsDirector)
            {
                ReportTitle = "Отчеты руководителя";
                ReportDescription = "Общая рабочая картина по проектам и задачам";
            }
            else
            {
                ReportTitle = "Отчеты ответственного";
                ReportDescription = "Отчет по доступным проектам и задачам";
            }

            var projectsQuery = _db.Projects
                .Include(p => p.Members)
                .AsQueryable();

            if (archive == "archived")
            {
                projectsQuery = projectsQuery.Where(p => p.IsArchived);
            }
            else
            {
                projectsQuery = projectsQuery.Where(p => !p.IsArchived);
            }

            if (IsAdmin || IsDirector)
            {
            }
            else if (IsManager && CurrentUserId.HasValue)
            {
                var userId = CurrentUserId.Value;

                projectsQuery = projectsQuery.Where(p =>
                    p.OwnerId == userId ||
                    p.Members.Any(m => m.UserId == userId));
            }
            else
            {
                projectsQuery = projectsQuery.Where(p => false);
            }

            var availableProjects = projectsQuery
                .OrderBy(p => p.Name)
                .ToList();

            ProjectOptions = availableProjects
                .Select(p => new SelectListItem
                {
                    Value = p.Id.ToString(),
                    Text = p.Name
                })
                .ToList();

            var allowedProjectIds = availableProjects
                .Select(p => p.Id)
                .ToList();

            var tasksQuery = _db.Tasks
                .Include(t => t.Project)
                .Include(t => t.AssigneeUser)
                .Where(t =>
                    t.ProjectId.HasValue &&
                    allowedProjectIds.Contains(t.ProjectId.Value))
                .AsQueryable();

            if (projectId.HasValue)
            {
                if (allowedProjectIds.Contains(projectId.Value))
                {
                    tasksQuery = tasksQuery.Where(t => t.ProjectId == projectId.Value);
                    availableProjects = availableProjects
                        .Where(p => p.Id == projectId.Value)
                        .ToList();
                }
                else
                {
                    tasksQuery = tasksQuery.Where(t => false);
                    availableProjects = new List<Project>();
                }
            }

            var tasks = tasksQuery.ToList();

            ProjectsTotal = availableProjects.Count;
            TasksTotal = tasks.Count;
            TasksActive = tasks.Count(t => t.Status != "done");
            TasksDone = tasks.Count(t => t.Status == "done");
            TasksOverdue = tasks.Count(t =>
                t.DueDate.HasValue &&
                t.DueDate.Value.Date < DateTime.Today &&
                t.Status != "done");
            TotalHours = tasks.Sum(t => t.SpentHours);

            EmployeeRows = tasks
                .GroupBy(t => t.AssigneeUser != null ? t.AssigneeUser.Name : "Не назначен")
                .Select(g => new ReportRow
                {
                    Employee = g.Key,
                    TotalTasks = g.Count(),
                    ActiveTasks = g.Count(x => x.Status != "done"),
                    DoneTasks = g.Count(x => x.Status == "done"),
                    TotalHours = g.Sum(x => x.SpentHours)
                })
                .OrderBy(r => r.Employee)
                .ToList();

            ProjectRows = availableProjects
                .Select(p =>
                {
                    var projectTasks = tasks
                        .Where(t => t.ProjectId == p.Id)
                        .ToList();

                    return new ReportRow
                    {
                        Employee = p.Name,
                        TotalTasks = projectTasks.Count,
                        ActiveTasks = projectTasks.Count(t => t.Status != "done"),
                        DoneTasks = projectTasks.Count(t => t.Status == "done"),
                        TotalHours = projectTasks.Sum(t => t.SpentHours)
                    };
                })
                .OrderBy(r => r.Employee)
                .ToList();
        }

        private byte[] GeneratePdfReport()
        {
            var createdAt = DateTime.Now.ToString("dd.MM.yyyy HH:mm");
            var projectFilterText = projectId.HasValue
                ? ProjectOptions.FirstOrDefault(p => p.Value == projectId.Value.ToString())?.Text ?? "Выбранный проект"
                : "Все проекты";

            var archiveText = archive == "archived" ? "Архивные проекты" : "Обычные проекты";

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                    page.Header().Column(column =>
                    {
                        column.Item().Text(ReportTitle).FontSize(18).SemiBold();
                        column.Item().Text($"Дата формирования: {createdAt}").FontSize(9);
                        column.Item().Text($"Фильтр: {archiveText}, {projectFilterText}").FontSize(9);
                    });

                    page.Content().PaddingTop(15).Column(column =>
                    {
                        column.Spacing(12);

                        column.Item().Text("Итоги").FontSize(14).SemiBold();

                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                            });

                            AddHeaderCell(table, "Проекты");
                            AddHeaderCell(table, "Задачи");
                            AddHeaderCell(table, "Активные");
                            AddHeaderCell(table, "Заверш.");
                            AddHeaderCell(table, "Просроч.");
                            AddHeaderCell(table, "Часы");

                            AddBodyCell(table, ProjectsTotal.ToString());
                            AddBodyCell(table, TasksTotal.ToString());
                            AddBodyCell(table, TasksActive.ToString());
                            AddBodyCell(table, TasksDone.ToString());
                            AddBodyCell(table, TasksOverdue.ToString());
                            AddBodyCell(table, TotalHours.ToString());
                        });

                        column.Item().Text("По сотрудникам").FontSize(14).SemiBold();

                        if (EmployeeRows.Count == 0)
                        {
                            column.Item().Text("Данных пока нет");
                        }
                        else
                        {
                            column.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(3);
                                    columns.RelativeColumn();
                                    columns.RelativeColumn();
                                    columns.RelativeColumn();
                                    columns.RelativeColumn();
                                });

                                AddHeaderCell(table, "Сотрудник");
                                AddHeaderCell(table, "Всего");
                                AddHeaderCell(table, "Активные");
                                AddHeaderCell(table, "Заверш.");
                                AddHeaderCell(table, "Часы");

                                foreach (var row in EmployeeRows)
                                {
                                    AddBodyCell(table, row.Employee);
                                    AddBodyCell(table, row.TotalTasks.ToString());
                                    AddBodyCell(table, row.ActiveTasks.ToString());
                                    AddBodyCell(table, row.DoneTasks.ToString());
                                    AddBodyCell(table, row.TotalHours.ToString());
                                }
                            });
                        }

                        column.Item().Text("По проектам").FontSize(14).SemiBold();

                        if (ProjectRows.Count == 0)
                        {
                            column.Item().Text("Данных пока нет");
                        }
                        else
                        {
                            column.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(3);
                                    columns.RelativeColumn();
                                    columns.RelativeColumn();
                                    columns.RelativeColumn();
                                    columns.RelativeColumn();
                                });

                                AddHeaderCell(table, "Проект");
                                AddHeaderCell(table, "Всего");
                                AddHeaderCell(table, "Активные");
                                AddHeaderCell(table, "Заверш.");
                                AddHeaderCell(table, "Часы");

                                foreach (var row in ProjectRows)
                                {
                                    AddBodyCell(table, row.Employee);
                                    AddBodyCell(table, row.TotalTasks.ToString());
                                    AddBodyCell(table, row.ActiveTasks.ToString());
                                    AddBodyCell(table, row.DoneTasks.ToString());
                                    AddBodyCell(table, row.TotalHours.ToString());
                                }
                            });
                        }
                    });

                    page.Footer()
                        .AlignCenter()
                        .Text(text =>
                        {
                            text.Span("RazorPM · ");
                            text.CurrentPageNumber();
                            text.Span(" / ");
                            text.TotalPages();
                        });
                });
            });

            return document.GeneratePdf();
        }

        private byte[] GenerateRowsPdfReport(string title, string firstColumnTitle, List<ReportRow> rows)
        {
            var createdAt = DateTime.Now.ToString("dd.MM.yyyy HH:mm");

            var projectFilterText = projectId.HasValue
                ? ProjectOptions.FirstOrDefault(p => p.Value == projectId.Value.ToString())?.Text ?? "Выбранный проект"
                : "Все проекты";

            var archiveText = archive == "archived" ? "Архивные проекты" : "Обычные проекты";

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                    page.Header().Column(column =>
                    {
                        column.Item().Text(title).FontSize(18).SemiBold();
                        column.Item().Text($"Дата формирования: {createdAt}").FontSize(9);
                        column.Item().Text($"Фильтр: {archiveText}, {projectFilterText}").FontSize(9);
                    });

                    page.Content().PaddingTop(15).Column(column =>
                    {
                        column.Spacing(12);

                        column.Item().Text("Итоги").FontSize(14).SemiBold();

                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                            });

                            AddHeaderCell(table, "Проекты");
                            AddHeaderCell(table, "Задачи");
                            AddHeaderCell(table, "Активные");
                            AddHeaderCell(table, "Заверш.");
                            AddHeaderCell(table, "Просроч.");
                            AddHeaderCell(table, "Часы");

                            AddBodyCell(table, ProjectsTotal.ToString());
                            AddBodyCell(table, TasksTotal.ToString());
                            AddBodyCell(table, TasksActive.ToString());
                            AddBodyCell(table, TasksDone.ToString());
                            AddBodyCell(table, TasksOverdue.ToString());
                            AddBodyCell(table, TotalHours.ToString());
                        });

                        column.Item().Text(title).FontSize(14).SemiBold();

                        if (rows.Count == 0)
                        {
                            column.Item().Text("Данных пока нет");
                        }
                        else
                        {
                            column.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(3);
                                    columns.RelativeColumn();
                                    columns.RelativeColumn();
                                    columns.RelativeColumn();
                                    columns.RelativeColumn();
                                });

                                AddHeaderCell(table, firstColumnTitle);
                                AddHeaderCell(table, "Всего");
                                AddHeaderCell(table, "Активные");
                                AddHeaderCell(table, "Заверш.");
                                AddHeaderCell(table, "Часы");

                                foreach (var row in rows)
                                {
                                    AddBodyCell(table, row.Employee);
                                    AddBodyCell(table, row.TotalTasks.ToString());
                                    AddBodyCell(table, row.ActiveTasks.ToString());
                                    AddBodyCell(table, row.DoneTasks.ToString());
                                    AddBodyCell(table, row.TotalHours.ToString());
                                }
                            });
                        }
                    });

                    page.Footer()
                        .AlignCenter()
                        .Text(text =>
                        {
                            text.Span("RazorPM · ");
                            text.CurrentPageNumber();
                            text.Span(" / ");
                            text.TotalPages();
                        });
                });
            });

            return document.GeneratePdf();
        }

        private static void AddHeaderCell(TableDescriptor table, string text)
        {
            table.Cell()
                .Background(Colors.Grey.Lighten3)
                .Border(1)
                .BorderColor(Colors.Grey.Lighten1)
                .Padding(5)
                .Text(text)
                .SemiBold();
        }

        private static void AddBodyCell(TableDescriptor table, string text)
        {
            table.Cell()
                .BorderBottom(1)
                .BorderColor(Colors.Grey.Lighten2)
                .Padding(5)
                .Text(text ?? "");
        }

        private static string Csv(string? value)
        {
            value ??= "";
            value = value.Replace("\"", "\"\"");

            return $"\"{value}\"";
        }
    }
}
