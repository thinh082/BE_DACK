using BE_DACK.Models.Entities;
using BE_DACK.Models.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using WebAppDoCongNghe.Service;

namespace BE_DACK.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductController : ControllerBase
    {
        private readonly DACKContext _context;
        private readonly ICloudinaryService _cloudinaryService;

        public ProductController(DACKContext context, ICloudinaryService cloudinaryService)
        {
            _context = context;
            _cloudinaryService = cloudinaryService;
        }
        [HttpGet("DanhSachSanPham")]
        public IActionResult DanhSachSanPham()
        {
           
            try
            {
                var danhSachSanPham = _context.Products
                    .Select(p => new
                    {
                        id = p.Id,
                        tenSp = p.TenSp,
                        moTa = p.MoTa,
                        gia = p.Gia,
                        soLuongConLaiTrongKho = p.SoLuongConLaiTrongKho,
                        categoryId = p.CategoryId,
                        hinhAnh = p.ProductImages.Select(p => new
                        {
                            p.Id,
                            p.ProductId,
                            p.HinhAnh
                        })
                    })
                    .ToList();

                return Ok(new
                {
                    success = true,
                    message = "Lấy danh sách sản phẩm thành công",
                    data = danhSachSanPham,
                    total = danhSachSanPham.Count
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi lấy danh sách sản phẩm",
                    error = ex.Message
                });
            }
        }
        [HttpGet("DanhSachDanhMuc")]
        public IActionResult DanhSachDanhMuc()
        {
            try
            {
                var danhSachDanhMuc = _context.Categories
                    .Select(c => new
                    {
                        id = c.Id,
                        tenDanhMuc = c.TenDanhMucSp,
                        moTa = c.MoTaDanhMuc
                    })
                    .ToList();

                return Ok(new
                {
                    success = true,
                    message = "Lấy danh sách danh mục thành công",
                    data = danhSachDanhMuc,
                    total = danhSachDanhMuc.Count
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi lấy danh sách danh mục",
                    error = ex.Message
                });
            }
        }

        [HttpGet("DanhSachSanPhamTheoDanhMuc/{categoryId}")]
        public IActionResult DanhSachSanPhamTheoDanhMuc(int categoryId)
        {
            try
            {
                var sanPhamTheoDanhMuc = _context.Products
                    .Where(p => p.CategoryId == categoryId)
                    .Select(p => new
                    {
                        id = p.Id,
                        tenSp = p.TenSp,
                        moTa = p.MoTa,
                        gia = p.Gia,
                        soLuongConLaiTrongKho = p.SoLuongConLaiTrongKho,
                        categoryId = p.CategoryId,
                        hinhAnh = p.ProductImages.Select(p => new
                        {
                            p.Id,
                            p.ProductId,
                            p.HinhAnh
                        })
                    })
                    .ToList();

                if (sanPhamTheoDanhMuc == null || sanPhamTheoDanhMuc.Count == 0)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Không tìm thấy sản phẩm nào thuộc danh mục này"
                    });
                }

                return Ok(new
                {
                    success = true,
                    message = "Lấy danh sách sản phẩm theo danh mục thành công",
                    data = sanPhamTheoDanhMuc,
                    total = sanPhamTheoDanhMuc.Count
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi lấy danh sách sản phẩm theo danh mục",
                    error = ex.Message
                });
            }
        }
        

        [HttpDelete("XoaSanPham/{id}")]
        public async Task<IActionResult> XoaSanPham(int id)
        {
            try
            {
                var product = await _context.Products.FindAsync(id);
                if (product == null)
                    return NotFound(new { success = false, message = "Không tìm thấy sản phẩm" });

                _context.Products.Remove(product);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Xóa sản phẩm thành công" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi khi xóa sản phẩm", error = ex.Message });
            }
        }
        // Thêm vào ProductController

        [HttpGet("ChiTietSanPham/{id}")]
        public async Task<IActionResult> ChiTietSanPham(int id)
        {
            try
            {
                var sanPham = await _context.Products
                    .Include(p => p.Category)
                    .Include(p => p.ProductImages)
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (sanPham == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Không tìm thấy sản phẩm"
                    });
                }

                return Ok(new
                {
                    success = true,
                    message = "Lấy chi tiết sản phẩm thành công",
                    data = new
                    {
                        id = sanPham.Id,
                        tenSp = sanPham.TenSp,
                        moTa = sanPham.MoTa,
                        gia = sanPham.Gia,
                        soLuongConLaiTrongKho = sanPham.SoLuongConLaiTrongKho,
                        conHang = sanPham.SoLuongConLaiTrongKho > 0,
                        categoryId = sanPham.CategoryId,
                        danhMuc = sanPham.Category != null ? new
                        {
                            id = sanPham.Category.Id,
                            tenDanhMuc = sanPham.Category.TenDanhMucSp,
                            moTa = sanPham.Category.MoTaDanhMuc
                        } : null,
                        hinhAnh = sanPham.ProductImages.Select(img => new
                        {
                            id = img.Id,
                            productId = img.ProductId,
                            url = img.HinhAnh
                        }).ToList()
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi lấy chi tiết sản phẩm",
                    error = ex.Message
                });
            }
        }

        [HttpPost("ThemSanPham")]
        public async Task<IActionResult> ThemSanPham([FromForm] ThemSanPhamRequest model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    Success = false,
                    Message = "Dữ liệu không hợp lệ",
                    Errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage))
                });
            }

            using var trans = await _context.Database.BeginTransactionAsync();
            try
            {
                var sp = new Product
                {
                    TenSp = model.TenSp,
                    CategoryId = model.CategoryId,
                    MoTa = model.MoTa,
                    Gia = model.Gia,
                    SoLuongConLaiTrongKho = model.SoLuongConLaiTrongKho,
                };

                _context.Products.Add(sp);
                await _context.SaveChangesAsync();

                // Upload hình ảnh
                foreach (var file in model.HinhAnh)
                {
                    var url = await _cloudinaryService.UploadImageAsync(file, "SanPham");
                    if (!string.IsNullOrEmpty(url))
                    {
                        _context.ProductImages.Add(new ProductImage
                        {
                            ProductId = sp.Id,
                            HinhAnh = url
                        });
                    }
                }

                await _context.SaveChangesAsync();
                await trans.CommitAsync();

                return Ok(new
                {
                    Success = true,
                    Message = "Thêm sản phẩm thành công",
                    Data = new { ProductId = sp.Id }
                });
            }
            catch (Exception ex)
            {
                await trans.RollbackAsync();
                return BadRequest(new
                {
                    Success = false,
                    Message = "Có lỗi xảy ra khi thêm sản phẩm",
                    Error = ex.Message
                });
            }
        }
        [HttpPut("SuaSanPham")]  // Hoặc [HttpPatch("SuaSanPham")]
        public async Task<IActionResult> SuaSanPham([FromForm] SuaSanPhamRequest model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    Success = false,
                    Message = "Dữ liệu không hợp lệ",
                    Errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage))
                });
            }

            using var trans = await _context.Database.BeginTransactionAsync();
            try
            {
                var product = await _context.Products.FirstOrDefaultAsync(r => r.Id == model.Id);

                if (product == null)
                {
                    return NotFound(new
                    {
                        Success = false,
                        Message = $"Không tìm thấy sản phẩm với ID = {model.Id}"
                    });
                }

                // CHỈ cập nhật những field có giá trị (không null/empty)
                if (!string.IsNullOrWhiteSpace(model.TenSp))
                {
                    product.TenSp = model.TenSp;
                }

                // Cho phép xóa mô tả bằng cách gửi chuỗi rỗng
                if (model.MoTa != null)
                {
                    product.MoTa = string.IsNullOrWhiteSpace(model.MoTa) ? null : model.MoTa;
                }

                if (model.Gia.HasValue)
                {
                    product.Gia = model.Gia.Value;
                }

                if (model.SoLuongConLaiTrongKho.HasValue)
                {
                    product.SoLuongConLaiTrongKho = model.SoLuongConLaiTrongKho.Value;
                }

                // Cho phép xóa CategoryId bằng cách gửi 0 hoặc -1
                if (model.CategoryId.HasValue)
                {
                    product.CategoryId = model.CategoryId.Value <= 0 ? null : model.CategoryId.Value;
                }

                _context.Products.Update(product);
                await _context.SaveChangesAsync();

                // Thêm hình ảnh mới nếu có (giữ nguyên ảnh cũ)
                if (model.HinhAnh != null && model.HinhAnh.Any())
                {
                    foreach (var file in model.HinhAnh)
                    {
                        var url = await _cloudinaryService.UploadImageAsync(file, "SanPham");
                        if (!string.IsNullOrEmpty(url))
                        {
                            _context.ProductImages.Add(new ProductImage
                            {
                                ProductId = product.Id,
                                HinhAnh = url
                            });
                        }
                    }
                    await _context.SaveChangesAsync();
                }

                await trans.CommitAsync();

                return Ok(new
                {
                    Success = true,
                    Message = "Cập nhật sản phẩm thành công",
                    Data = product
                });
            }
            catch (Exception ex)
            {
                await trans.RollbackAsync();
                return BadRequest(new
                {
                    Success = false,
                    Message = "Có lỗi xảy ra khi cập nhật sản phẩm",
                    Error = ex.Message
                });
            }
        }

        [HttpDelete("XoaHinhAnh/{imageId}")]
        public async Task<IActionResult> XoaHinhAnh(int imageId)
        {
            try
            {
                var image = await _context.ProductImages.FindAsync(imageId);
                if (image == null)
                {
                    return NotFound(new { Success = false, Message = "Không tìm thấy hình ảnh" });
                }

                // Xóa ảnh trên Cloudinary (nếu cần)
                // await _cloudinaryService.DeleteImageAsync(image.HinhAnh);

                _context.ProductImages.Remove(image);
                await _context.SaveChangesAsync();

                return Ok(new { Success = true, Message = "Xóa hình ảnh thành công" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Success = false, Message = ex.Message });
            }
        }

        [HttpGet("LayDanhSachHinhAnh/{productId}")]
        public async Task<IActionResult> LayDanhSachHinhAnh(int productId)
        {
            var images = await _context.ProductImages
                .Where(x => x.ProductId == productId)
                .Select(x => new { x.Id, x.HinhAnh })
                .ToListAsync();

            return Ok(new { Success = true, Data = images });
        }

        [HttpGet("LocSanPhamTheoGia")]
        public IActionResult LocSanPhamTheoGia([FromQuery] decimal? giaMin, [FromQuery] decimal? giaMax,
            [FromQuery] int? categoryId)
        {
            try
            {
                var query = _context.Products
                    .Include(p => p.Category)
                    .Include(p => p.ProductImages)
                    .AsQueryable();

                // Lọc theo khoảng giá
                if (giaMin.HasValue)
                {
                    query = query.Where(p => p.Gia >= giaMin.Value);
                }

                if (giaMax.HasValue)
                {
                    query = query.Where(p => p.Gia <= giaMax.Value);
                }

                // Lọc thêm theo danh mục (nếu có)
                if (categoryId.HasValue)
                {
                    query = query.Where(p => p.CategoryId == categoryId.Value);
                }

                // Lấy tất cả sản phẩm, sắp xếp theo giá tăng dần
                var danhSachSanPham = query
                    .OrderBy(p => p.Gia)
                    .Select(p => new
                    {
                        id = p.Id,
                        tenSp = p.TenSp,
                        moTa = p.MoTa,
                        gia = p.Gia,
                        soLuongConLaiTrongKho = p.SoLuongConLaiTrongKho,
                        conHang = p.SoLuongConLaiTrongKho > 0,
                        categoryId = p.CategoryId,
                        danhMuc = p.Category != null ? new
                        {
                            id = p.Category.Id,
                            tenDanhMuc = p.Category.TenDanhMucSp
                        } : null,
                        hinhAnh = p.ProductImages.Select(img => new
                        {
                            id = img.Id,
                            productId = img.ProductId,
                            url = img.HinhAnh
                        }).ToList()
                    })
                    .ToList();

                return Ok(new
                {
                    success = true,
                    message = "Lọc sản phẩm theo giá thành công",
                    filters = new
                    {
                        giaMin = giaMin,
                        giaMax = giaMax,
                        categoryId = categoryId
                    },
                    total = danhSachSanPham.Count,
                    data = danhSachSanPham
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi lọc sản phẩm theo giá",
                    error = ex.Message
                });
            }
        }

        // API lấy khoảng giá min-max
        [HttpGet("KhoangGia")]
        public IActionResult KhoangGia()
        {
            try
            {
                var sanPham = _context.Products.AsQueryable();

                if (!sanPham.Any())
                {
                    return Ok(new
                    {
                        success = true,
                        message = "Chưa có sản phẩm nào",
                        data = new
                        {
                            giaMin = 0,
                            giaMax = 0
                        }
                    });
                }

                var giaMin = sanPham.Min(p => p.Gia);
                var giaMax = sanPham.Max(p => p.Gia);

                return Ok(new
                {
                    success = true,
                    message = "Lấy khoảng giá thành công",
                    data = new
                    {
                        giaMin = giaMin,
                        giaMax = giaMax
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi lấy khoảng giá",
                    error = ex.Message
                });
            }
        }

        // API lọc theo nhiều tiêu chí
        [HttpGet("LocVaTimKiem")]
        public IActionResult LocVaTimKiem(
            [FromQuery] string? keyword,
            [FromQuery] int? categoryId,
            [FromQuery] decimal? giaMin,
            [FromQuery] decimal? giaMax,
            [FromQuery] string? sapXep = "gia-tang", // gia-tang, gia-giam, ten-az, ten-za, moi-nhat
            [FromQuery] bool? conHang = null)
        {
            try
            {
                var query = _context.Products
                    .Include(p => p.Category)
                    .Include(p => p.ProductImages)
                    .AsQueryable();

                // Lọc theo từ khóa
                if (!string.IsNullOrEmpty(keyword))
                {
                    keyword = keyword.ToLower().Trim();
                    query = query.Where(p => p.TenSp.ToLower().Contains(keyword)
                        || (p.MoTa != null && p.MoTa.ToLower().Contains(keyword)));
                }

                // Lọc theo danh mục
                if (categoryId.HasValue)
                {
                    query = query.Where(p => p.CategoryId == categoryId.Value);
                }

                // Lọc theo khoảng giá
                if (giaMin.HasValue)
                {
                    query = query.Where(p => p.Gia >= giaMin.Value);
                }
                if (giaMax.HasValue)
                {
                    query = query.Where(p => p.Gia <= giaMax.Value);
                }

                // Lọc theo tình trạng hàng
                if (conHang.HasValue)
                {
                    if (conHang.Value)
                    {
                        query = query.Where(p => p.SoLuongConLaiTrongKho > 0);
                    }
                    else
                    {
                        query = query.Where(p => p.SoLuongConLaiTrongKho == 0);
                    }
                }

                // Sắp xếp
                query = sapXep?.ToLower() switch
                {
                    "gia-giam" => query.OrderByDescending(p => p.Gia),
                    "ten-az" => query.OrderBy(p => p.TenSp),
                    "ten-za" => query.OrderByDescending(p => p.TenSp),
                    "moi-nhat" => query.OrderByDescending(p => p.Id),
                    _ => query.OrderBy(p => p.Gia) // Mặc định: giá tăng dần
                };

                var danhSachSanPham = query
                    .Select(p => new
                    {
                        id = p.Id,
                        tenSp = p.TenSp,
                        moTa = p.MoTa,
                        gia = p.Gia,
                        soLuongConLaiTrongKho = p.SoLuongConLaiTrongKho,
                        conHang = p.SoLuongConLaiTrongKho > 0,
                        categoryId = p.CategoryId,
                        danhMuc = p.Category != null ? new
                        {
                            id = p.Category.Id,
                            tenDanhMuc = p.Category.TenDanhMucSp
                        } : null,
                        hinhAnh = p.ProductImages.Select(img => new
                        {
                            id = img.Id,
                            productId = img.ProductId,
                            url = img.HinhAnh
                        }).ToList()
                    })
                    .ToList();

                return Ok(new
                {
                    success = true,
                    message = "Lọc và tìm kiếm sản phẩm thành công",
                    filters = new
                    {
                        keyword = keyword,
                        categoryId = categoryId,
                        giaMin = giaMin,
                        giaMax = giaMax,
                        sapXep = sapXep,
                        conHang = conHang
                    },
                    total = danhSachSanPham.Count,
                    data = danhSachSanPham
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi lọc và tìm kiếm sản phẩm",
                    error = ex.Message
                });
            }
        }

    }
    /*public class ProductCreateDto
    {
        public string TenSp { get; set; } = null!;
        public string? MoTa { get; set; }
        public decimal Gia { get; set; }
        public int SoLuongConLaiTrongKho { get; set; }
        public int? CategoryId { get; set; }
        public string? ImageUrl { get; set; }
    }*/
    // Model cho Thêm mới
    public class ThemSanPhamRequest
    {
        [Required(ErrorMessage = "Tên sản phẩm là bắt buộc")]
        public string TenSp { get; set; }

        public string? MoTa { get; set; }

        [Required(ErrorMessage = "Giá là bắt buộc")]
        [Range(0, double.MaxValue, ErrorMessage = "Giá phải lớn hơn 0")]
        public decimal Gia { get; set; }

        [Required(ErrorMessage = "Số lượng là bắt buộc")]
        [Range(0, int.MaxValue, ErrorMessage = "Số lượng phải lớn hơn hoặc bằng 0")]
        public int SoLuongConLaiTrongKho { get; set; }

        public int? CategoryId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn ít nhất 1 hình ảnh")]
        public List<IFormFile> HinhAnh { get; set; }
    }

    // Model cho Cập nhật - CHỈ CẦN ID
    public class SuaSanPhamRequest
    {
        [Required(ErrorMessage = "ID sản phẩm là bắt buộc")]
        public int Id { get; set; }

        public string? TenSp { get; set; }

        public string? MoTa { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Giá phải lớn hơn 0")]
        public decimal? Gia { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Số lượng phải lớn hơn hoặc bằng 0")]
        public int? SoLuongConLaiTrongKho { get; set; }

        public int? CategoryId { get; set; }

        // Null = không thêm ảnh mới
        public List<IFormFile>? HinhAnh { get; set; }
    }
}
