﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DotNetNote.Models
{
    public interface IBoardRepository
    {
        /// <summary>
        /// Board 리스트 출력
        /// </summary>
        List<Board> GetBoards(int page);

        /// <summary>
        /// 글 상세보기
        /// </summary>
        /// <param name="id">글번호</param>
        Board GetDetailById(int id);

        /// <summary>
        /// 글 작성하기
        /// </summary>
        void WriteArticle(Board model);
        
        /// <summary>
        /// 글 삭제하기
        /// </summary>
        int DeleteArticle(int id, string password);

        /// <summary>
        /// 글 수정하기
        /// </summary>
        int UpdateArticle(Board board);
    }
}
