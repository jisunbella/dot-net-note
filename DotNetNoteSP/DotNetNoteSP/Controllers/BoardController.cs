﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using DotNetNote.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DotNetNote.Controllers
{
    public class BoardController : Controller
    {
        private IHostingEnvironment _environment; //환경 변수
        private IBoardRepository _repository; //게시판 리포지토리

        /// <summary>
        /// 현재 보여줄 페이지 번호
        /// </summary>
        public int PageIndex { get; set; } = 0;

        public BoardController(IHostingEnvironment environment, IBoardRepository repository)
        {
            _environment = environment;
            _repository = repository;
        }

        // 공통 속성: 검색 모드: 검색 모드이면 true, 그렇지 않으면 false.
        public bool SearchMode { get; set; } = false;
        public string SearchField { get; set; } // 필드: Name, Title, Content
        public string SearchQuery { get; set; } // 검색 내용

        /// <summary>
        /// 게시판 리스트
        /// </summary>
        /// <returns>Index.cshtml</returns>
        public IActionResult Index()
        {
            // 검색 모드 결정: ?SearchField=Name&SearchQuery=검색어 //쿼리스트링 방식
            SearchMode = (
                !string.IsNullOrEmpty(Request.Query["SearchField"]) &&
                !string.IsNullOrEmpty(Request.Query["SearchQuery"])
            );

            // 검색 환경이면 속성에 저장
            if (SearchMode)
            {
                SearchField = Request.Query["SearchField"].ToString();
                SearchQuery = Request.Query["SearchQuery"].ToString();
            }

            //쿼리스트링에 따른 페이지 보여주기
            if (!string.IsNullOrEmpty(Request.Query["Page"].ToString()))
            {
                // Page는 보여지는 쪽은 1, 2, 3, ... 코드단에서는 0, 1, 2, ...
                PageIndex = Convert.ToInt32(Request.Query["Page"]) - 1;
            }

            IEnumerable<Board> board;
            if (!SearchMode)
            {
                board = _repository.GetBoards(PageIndex);
            }
            else
            {
                board = _repository.GetSearchAll(PageIndex, SearchField, SearchQuery);
            }

            //주요 정보를 뷰 페이지로 전송
            ViewBag.SearchMode = SearchMode;
            ViewBag.SearchField = SearchField;
            ViewBag.SearchQuery = SearchQuery;

            // 페이저 컨트롤 적용
            ViewBag.PageModel = new PagerBase
            {
                Url = "Board/Index",
                PageSize = 10,
                PageNumber = PageIndex + 1,

                SearchMode = SearchMode,
                SearchField = SearchField,
                SearchQuery = SearchQuery
            };

            return View(board);
        }

        /// <summary>
        /// 글 상세보기
        /// </summary>
        /// <param name="id">글 번호</param>
        public IActionResult Detail(int id)
        {
            //넘어온 id에 해당하는 레코드 읽어서 board에 바인딩
            var board = _repository.GetDetailById(id);

            string content = board.Content;
            ViewBag.Content = content;

            // 첨부된 파일 확인
            if (String.IsNullOrEmpty(board.FileName))
            {
                ViewBag.FileName = "(업로드된 파일이 없습니다.)";
            }
            else
            {
                // 이미지 미리보기:
                if (Dul.BoardLibrary.IsPhoto(board.FileName))
                {
                    ViewBag.FileName = board.FileName;
                    ViewBag.ImageDown = $"<img src=\'/files/{board.FileName}\'><br />";
                }
            }
            return View(board);
        }

        /// <summary>
        /// 글쓰기
        /// </summary>
        [HttpGet]
        public IActionResult Write()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Write(Board board, ICollection<IFormFile> files)
        {
            //파일 업로드 처리 시작
            string fileName = String.Empty;
            int fileSize = 0;

            var uploadFolder = Path.Combine(_environment.WebRootPath, "files");

            foreach (var file in files)
            {
                if (file.Length > 0)
                {
                    fileSize = Convert.ToInt32(file.Length);
                    // 파일명 중복 처리
                    fileName = Dul.FileUtility.GetFileNameWithNumbering(
                        uploadFolder, Path.GetFileName(
                            ContentDispositionHeaderValue.Parse(
                                file.ContentDisposition).FileName.Trim('"')));
                    // 파일 업로드
                    using (var fileStream = new FileStream(
                        Path.Combine(uploadFolder, fileName), FileMode.Create))
                    {
                        await file.CopyToAsync(fileStream);
                    }
                }
            }

            Board b = new Board();

            b.Title = board.Title;
            b.Name = board.Name;
            b.Content = board.Content;
            b.Password = board.Password;
            b.FileName = fileName;
            b.FileSize = fileSize;

            _repository.WriteArticle(b); //데이터 저장

            TempData["Message"] = "데이터가 저장되었습니다.";

            return RedirectToAction("Index"); // 저장 후 리스트 페이지로 이동
        }

        /// <summary>
        /// 게시판 삭제 폼
        /// </summary>
        [HttpGet]
        public IActionResult Delete(int id)
        {
            ViewBag.Id = id;
            return View();
        }

        /// <summary>
        /// 게시판 삭제 처리
        /// </summary>
        [HttpPost]
        public IActionResult Delete(int id, string Password)
        {
            if (_repository.DeleteArticle(id, Password) > 0)
            {
                TempData["Message"] = "데이터가 삭제되었습니다.";

                if (DateTime.Now.Second % 2 == 0)
                {
                    //[a] 삭제 후 특정 뷰 페이지로 이동
                    return RedirectToAction("DeleteCompleted");
                }
                else
                {
                    //[b] 삭제 후 Index 페이지로 이동
                    return RedirectToAction("Index");
                }
            }
            else
            {
                ViewBag.Message = "삭제되지 않았습니다. 비밀번호를 확인하세요.";
            }

            ViewBag.Id = id;
            return View();
        }

        /// <summary>
        /// 게시판 삭제 완료 후 추가적인 처리할 때 페이지
        /// </summary>
        public IActionResult DeleteCompleted()
        {
            return View();
        }

        /// <summary>
        /// 게시판 수정 폼
        /// </summary>
        [HttpGet]
        public IActionResult Edit(int id)
        {
            ViewBag.FormType = 1;
            ViewBag.TitleDescription = "글 수정 - 아래 항목을 수정하세요.";
            ViewBag.SaveButtonText = "수정";

            // 기존 데이터를 바인딩
            var board = _repository.GetDetailById(id);

            // 첨부된 파일명 및 파일크기 기록
            if (!String.IsNullOrEmpty(board.FileName))
            {
                if (board.FileName.Length > 1)
                {
                    ViewBag.FileName = board.FileName;
                    ViewBag.FileSize = board.FileSize;
                    ViewBag.FileNamePrevious =
                        $"기존에 업로드된 파일명: {board.FileName}";
                }
            }
            else
            {
                ViewBag.FileName = "";
                ViewBag.FileSize = 0;
            }

            return View(board);
        }

        /// <summary>
        /// 게시판 수정 처리 
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Edit(Board model, ICollection<IFormFile> files,
            int id, string previousFileName = "", int previousFileSize = 0)
        {
            ViewBag.FormType = 1;
            ViewBag.TitleDescription = "글 수정 - 아래 항목을 수정하세요.";
            ViewBag.SaveButtonText = "수정";

            string fileName = "";
            int fileSize = 0;

            if (previousFileName != null)
            {
                fileName = previousFileName;
                fileSize = previousFileSize;
            }

            //파일 업로드 처리 시작
            var uploadFolder = Path.Combine(_environment.WebRootPath, "files");

            foreach (var file in files)
            {
                if (file.Length > 0)
                {
                    fileSize = Convert.ToInt32(file.Length);
                    // 파일명 중복 처리
                    fileName = Dul.FileUtility.GetFileNameWithNumbering(
                        uploadFolder, Path.GetFileName(
                            ContentDispositionHeaderValue.Parse(
                                file.ContentDisposition).FileName.Trim('"')));
                    // 파일업로드
                    using (var fileStream = new FileStream(
                        Path.Combine(uploadFolder, fileName), FileMode.Create))
                    {
                        await file.CopyToAsync(fileStream);
                    }
                }
            }

            Board board = new Board();

            board.Id = id;
            board.Name = model.Name;
            board.Title = Dul.HtmlUtility.Encode(model.Title);
            board.Content = model.Content;
            board.Password = model.Password;
            board.FileName = fileName;
            board.FileSize = fileSize;

            int r = _repository.UpdateArticle(board); // 데이터베이스에 수정 적용
            if (r > 0)
            {
                TempData["Message"] = "수정되었습니다.";
                return RedirectToAction("Detail", new { Id = id });
            }
            else
            {
                ViewBag.ErrorMessage =
                    "업데이트가 되지 않았습니다. 암호를 확인하세요.";
                return View(board);
            }
        }
    }
}