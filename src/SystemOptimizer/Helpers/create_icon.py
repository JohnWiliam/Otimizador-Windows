import struct
import os

def create_ico(png_files, output_path):
    # Header: Reserved (2), Type (2=1 for ICO), Count (2)
    header = struct.pack('<HHH', 0, 1, len(png_files))
    
    directory_entries = []
    image_data = []
    
    current_offset = 6 + (16 * len(png_files))
    
    for png_path in png_files:
        with open(png_path, 'rb') as f:
            data = f.read()
            
        # Parse PNG header to get width/height
        # IHDR chunk starts at byte 8 (after signature)
        # Length (4), ChunkType (4), Width (4), Height (4), BitDepth(1), ColorType(1)...
        # Width is at index 16, Height at 20 (Big Endian)
        w = struct.unpack('>I', data[16:20])[0]
        h = struct.unpack('>I', data[20:24])[0]
        
        # ICO Width/Height: 0 means 256
        ico_w = 0 if w >= 256 else w
        ico_h = 0 if h >= 256 else h
        
        size = len(data)
        
        # Entry: Width(1), Height(1), Colors(1), Reserved(1), Planes(2), BPP(2), Size(4), Offset(4)
        entry = struct.pack('<BBBBHHII', ico_w, ico_h, 0, 0, 1, 32, size, current_offset)
        directory_entries.append(entry)
        image_data.append(data)
        
        current_offset += size
        
    with open(output_path, 'wb') as f:
        f.write(header)
        for entry in directory_entries:
            f.write(entry)
        for data in image_data:
            f.write(data)

if __name__ == '__main__':
    # Try to find resized parts first
    parts_dir = 'src/SystemOptimizer/Assets/IconParts'
    source_logo = 'src/SystemOptimizer/Assets/logo.png'
    output_ico = 'src/SystemOptimizer/Assets/icon.ico'
    
    files_to_pack = []
    
    if os.path.exists(parts_dir):
        # Prefer specific sizes if generated
        expected_sizes = [16, 32, 48, 64, 256]
        found = []
        for s in expected_sizes:
            p = os.path.join(parts_dir, f"{s}.png")
            if os.path.exists(p):
                found.append(p)
        if found:
            files_to_pack = found
            
    if not files_to_pack:
        # Fallback to single large image
        files_to_pack = [source_logo]
        
    print(f"Packing {len(files_to_pack)} images into {output_ico}")
    try:
        create_ico(files_to_pack, output_ico)
        print("Success.")
    except Exception as e:
        print(f"Error: {e}")
