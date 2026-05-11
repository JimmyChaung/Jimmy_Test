function generateBarChart(tableId, ChatId, x_name, y_name, se_name, x_feild, y_filed, xAxisColumnIndex = 0, yAxisColumnIndex = 1) {
    const table = document.getElementById(tableId);
    //判斷有無table
    if (!table) {
        console.error("Table With id ${tableId} no found");
        return;
    }

    const rows = table.getElementsByTagName('tbody')[0].getElementsByTagName('tr');

    const categories = []; // 儲存 X 軸標籤
    const seriesData = []; // 儲存 Y 軸數據


    //提取表格行、及數據
    //let row of rows?
    for (let row of rows) {
        const cells = row.getElementsByTagName('td');
        if (cells.length > xAxisColumnIndex && cells.length > yAxisColumnIndex) {
            categories.push(cells[x_feild].innerText);// x軸數據
            seriesData.push(cells[y_filed].innerText);//y軸數據
        }
    }



    //初始化chat

    const chartElement = document.getElementById(ChatId);


    if (!chartElement) {
        console.error(`Chart container with ID ${chartId} not found.`);
        return;
    }

    const chart = echarts.init(chartElement);

    // 設置圖表選項
    const chartOption = {

        tooltip: {},
        xAxis: {
            name: x_name,
            type: 'category',
            data: categories, // X 軸數據
            axisLabel: {
                interval: 0, // 每個標籤都顯示
                rotate: 45, // 旋轉標籤以避免重疊
            }
        },
        yAxis: {
            name: y_name,
            type: 'value'
        },
        series: [{
            name: se_name,
            type: 'bar',
            data: seriesData // Y 軸數據
        }]
    };

    // 使用指定的配置項和數據顯示圖表
    chart.setOption(chartOption);

}

function generateBarChart2(tableId, chartId, x_name, y_name, se_name, x_feild, y_filed, xAxisColumnIndex = 0, yAxisColumnIndex = 1) {
    // 初始化 DataTable
    const table = $('#' + tableId).DataTable();

    // 監聽 DataTable 切換頁面的事件
    table.on('draw', function () {
        // 提取當前頁面資料
        const currentPageData = table.rows({ page: 'current' }).nodes().toArray();

        const categories = []; // 儲存 X 軸標籤
        const seriesData = []; // 儲存 Y 軸數據

        // 提取表格行及數據
        currentPageData.forEach(function (row) {
            const cells = $(row).find('td');
            if (cells.length > xAxisColumnIndex && cells.length > yAxisColumnIndex) {
                categories.push(cells.eq(x_feild).text()); // x 軸數據
                seriesData.push(cells.eq(y_filed).text()); // y 軸數據
            }
        });

        // 初始化 ECharts 圖表
        const chartElement = document.getElementById(chartId);
        if (!chartElement) {
            console.error(`Chart container with ID ${chartId} not found.`);
            return;
        }

        const chart = echarts.init(chartElement);

        // 設置圖表選項
        const chartOption = {
            
            tooltip: {},
            xAxis: {
                name: x_name,
                type: 'category',
                data: categories, // X 軸數據
                axisLabel: {
                    interval: 0, // 每個標籤都顯示
                    rotate: 45, // 旋轉標籤以避免重疊
                }
            },
            yAxis: {
                name: y_name,
                type: 'value'
            },
            series: [{
                name: se_name,
                type: 'bar',
                data: seriesData, // Y 軸數據
                itemStyle: {
                    color: '#00cadc' 
                }
            }]
        };

        // 使用指定的配置項和數據顯示圖表
        chart.setOption(chartOption);
    });

    // 手動觸發一次 draw 事件以初始化圖表
    table.draw();
}
function generateStackBarChart(tableId, chartId, x_name, y_name,x_field,y_field,type_field) {
    // 初始化 DataTable
    const table = $('#' + tableId).DataTable();

    // 監聽 DataTable 切換頁面的事件
    table.on('draw', function () {
        // 提取當前頁面資料
        const currentPageData = table.rows({ page: 'current' }).nodes().toArray();

        const categories = []; // 儲存 X 軸標籤
        const openData = []; // 儲存 open 類型數據
        const closeData = []; // 儲存 close 類型數據
        const modifyData = []; // 儲存 modify 類型數據
        const deleteData = []; // 儲存 delete 類型數據pending
        const pendingData = [];
        const totalData = []; // 儲存 total 類型數據
        const loginSet = new Set(); // 用於收集獨特的 LOGIN

        // 提取表格行及數據
        currentPageData.forEach(function (row) {
            const cells = $(row).find('td');
            if (cells.length > 2) { 
                const login = cells.eq(x_field).text(); // x 軸數據
                const type = cells.eq(type_field).text(); // 請求類型
                const count = parseInt(cells.eq(y_field).text()) || 0; // 請求數量

                if (!loginSet.has(login)) {
                    categories.push(login); // 添加新 LOGIN
                    loginSet.add(login);
                    // 初始化數據
                    openData.push(0);
                    closeData.push(0);
                    modifyData.push(0);
                    deleteData.push(0);
                    pendingData.push(0);
                    totalData.push(0);
                }

                // 更新數據
                const index = categories.indexOf(login);
                if (type === 'open') {
                    openData[index] += count;
                } else if (type === 'close') {
                    closeData[index] += count;
                } else if (type === 'modify') {
                    modifyData[index] += count;
                } else if (type === 'delete') {
                    deleteData[index] += count;
                } else if (type === 'pending') {
                    pendingData[index] += count;
                } else if (type === 'total') {
                    totalData[index] += count; // 將 total 數據加起來
                }
            }
        });

        // 初始化 ECharts 圖表
        const chartElement = document.getElementById(chartId);
        if (!chartElement) {
            console.error(`Chart container with ID ${chartId} not found.`);
            return;
        }

        const chart = echarts.init(chartElement);

        // 設置圖表選項
        const chartOption = {

            tooltip: {
                trigger: 'axis'
            },
            legend: {
                data: ['Open', 'Close', 'Modify', 'Delete', 'Pending']
            },
            xAxis: {
                name: x_name,
                type: 'category',
                data: categories, // X 軸數據
                axisLabel: {
                    interval: 0, // 每個標籤都顯示
                    rotate: 45, // 旋轉標籤以避免重疊
                }
            },
            yAxis: {
                name: y_name,
                type: 'value'
            },
            series: [
                {
                    name: 'Open',
                    type: 'bar',
                    stack: 'types',
                    data: openData,
                    itemStyle: {
                        color: '#00cadc'
                    }
                },
                {
                    name: 'Close',
                    type: 'bar',
                    stack: 'types',
                    data: closeData,
                    itemStyle: {
                        color: '#ff7f50'
                    }
                },
                {
                    name: 'Modify',
                    type: 'bar',
                    stack: 'types',
                    data: modifyData,
                    itemStyle: {
                        color: '#32cd32'
                    }
                },
                {
                    name: 'Delete',
                    type: 'bar',
                    stack: 'types',
                    data: deleteData,
                    itemStyle: {
                        color: '#ffa500'
                    }
                },
                {
                    name: 'Pending',
                    type: 'bar',
                    stack: 'types',
                    data: pendingData,
                    itemStyle: {
                        color: '#c100ff'
                    }
                },
                /*
                {
                    name: 'Total',
                    type: 'bar',
                    data: totalData, // Total 數據
                    itemStyle: {
                        color: '#ff69b4'
                    }
                }
                */
            ]
        };

        // 使用指定的配置項和數據顯示圖表
        chart.setOption(chartOption);
    });

    // 手動觸發一次 draw 事件以初始化圖表
    table.draw();
}

function generateStackBarChart2(tableId, chartId, x_name, y_name, x_field, y_field, type_field) {
    // 初始化 DataTable
    const table = $('#' + tableId).DataTable();

    // 監聽 DataTable 切換頁面的事件
    table.on('draw', function () {
        // 提取當前頁面資料
        const currentPageData = table.rows({ page: 'current' }).nodes().toArray();

        const categories = []; 
        const loginData = [];
        const tradeData = []; 
        const DWData = []; 
        const positionData = []; 
        const balanceData = [];
        const achievableData = [];
        const loginSet = new Set(); // 用於收集獨特的 LOGIN

        // 提取表格行及數據
        currentPageData.forEach(function (row) {
            const cells = $(row).find('td');
            if (cells.length > 2) {
                const login = cells.eq(x_field).text(); 
                const type = cells.eq(type_field).text(); // 類型
                const count = parseInt(cells.eq(y_field).text()) || 0; // 數量

                if (!loginSet.has(login)) {
                    categories.push(login); // 添加新 LOGIN
                    loginSet.add(login);
                    // 初始化數據
                    loginData.push(0);
                    tradeData.push(0);
                    DWData.push(0);
                    positionData.push(0);
                    balanceData.push(0);
                    achievableData.push(0);
                }

                // 更新數據
                const index = categories.indexOf(login);
                if (type === 'login') {
                    loginData[index] += count;
                } else if (type === 'trade') {
                    tradeData[index] += count;
                } else if (type === 'DW') {
                    DWData[index] += count;
                } else if (type === 'position') {
                    positionData[index] += count;
                } else if (type === 'balance') {
                    balanceData[index] += count;
                } else if (type === 'achievable') {
                    achievableData[index] += count; 
                } 
            }
        });

        // 初始化 ECharts 圖表
        const chartElement = document.getElementById(chartId);
        if (!chartElement) {
            console.error(`Chart container with ID ${chartId} not found.`);
            return;
        }

        const chart = echarts.init(chartElement);

        // 設置圖表選項
        const chartOption = {

            tooltip: {
                trigger: 'axis'
            },
            legend: {
                data: ['Login', 'Trade', 'DW', 'Position', 'Balance','Achievable']
            },
            xAxis: {
                name: x_name,
                type: 'category',
                data: categories, 
                axisLabel: {
                    interval: 0,
                    rotate: 45, // 旋轉標籤以避免重疊
                }
            },
            yAxis: {
                name: y_name,
                type: 'value'
            },
            series: [
                {
                    name: 'Login',
                    type: 'bar',
                    stack: 'types',
                    data: loginData,
                    itemStyle: {
                        color: '#00cadc'
                    }
                },
                {
                    name: 'Trade',
                    type: 'bar',
                    stack: 'types',
                    data: tradeData,
                    itemStyle: {
                        color: '#ff7f50'
                    }
                },
                {
                    name: 'DW',
                    type: 'bar',
                    stack: 'types',
                    data: DWData,
                    itemStyle: {
                        color: '#32cd32'
                    }
                },
                {
                    name: 'Position',
                    type: 'bar',
                    stack: 'types',
                    data: positionData,
                    itemStyle: {
                        color: '#ffa500'
                    }
                },
                {
                    name: 'Balance',
                    type: 'bar',
                    stack: 'types',
                    data: balanceData,
                    itemStyle: {
                        color: '#c100ff'
                    }
                },
                
                {
                    name: 'Achievable',
                    type: 'bar',
                    stack: 'types',
                    data: achievableData,
                    itemStyle: {
                        color: '#ff69b4'
                    }
                }
                
            ]
        };

        chart.setOption(chartOption);
    });

    // 手動觸發一次 draw 事件以初始化圖表
    table.draw();
}


function generateGroupedBarChart(tableId, chartId, x_name, y_name, x_feild, y_filed, typeField) {
    // 初始化 DataTable
    const table = $('#' + tableId).DataTable();

    // 監聽 DataTable 切換頁面的事件
    table.on('draw', function () {
        // 提取當前頁面資料
        const currentPageData = table.rows({ page: 'current' }).nodes().toArray();

        const categories = []; // 儲存 X 軸標籤 (LOGIN)
        const typeData = {};   // 儲存 Y 軸數據，根據 LOGIN 分組並包含多個 TYPE 的數據

        // 初始化類型數據結構
        const types = ['achievable', 'balance', 'DW', 'login', 'position', 'trade'];

        // 提取表格行及數據
        currentPageData.forEach(function (row) {
            const cells = $(row).find('td');
            const login = cells.eq(x_feild).text(); // LOGIN (x軸)
            const orderCount = parseInt(cells.eq(y_filed).text()); // 訂單數量 (y軸)
            const type = cells.eq(typeField).text(); // TYPE (類型)

            // 如果 categories 還沒包含這個 login，就加上
            if (!categories.includes(login)) {
                categories.push(login);
                // 初始化這個 login 對應的每個 type 的數據為 0
                typeData[login] = {};
                types.forEach(type => {
                    typeData[login][type] = 0; // 初始為 0
                });
            }

            // 將訂單數量放入對應的類型數據中
            if (typeData[login][type] !== undefined) {
                typeData[login][type] += orderCount;
            }
        });

        // 組裝 seriesData 的格式，每個類型指定固定顏色
        const series = [
            { name: 'achievable', type: 'bar', data: categories.map(login => typeData[login]['achievable']), itemStyle: { color: '#00aaff' } },
            { name: 'balance', type: 'bar', data: categories.map(login => typeData[login]['balance']), itemStyle: { color: '#ffaa00' } },
            { name: 'DW', type: 'bar', data: categories.map(login => typeData[login]['DW']), itemStyle: { color: '#ff0000' } },
            { name: 'login', type: 'bar', data: categories.map(login => typeData[login]['login']), itemStyle: { color: '#00ff00' } },
            { name: 'position', type: 'bar', data: categories.map(login => typeData[login]['position']), itemStyle: { color: '#0000ff' } },
            { name: 'trade', type: 'bar', data: categories.map(login => typeData[login]['trade']), itemStyle: { color: '#ff00ff' } }
        ];

        // 初始化 ECharts 圖表
        const chartElement = document.getElementById(chartId);
        if (!chartElement) {
            console.error(`Chart container with ID ${chartId} not found.`);
            return;
        }

        const chart = echarts.init(chartElement);

        // 設置圖表選項
        const chartOption = {

            tooltip: {
                trigger: 'axis',
                axisPointer: {
                    type: 'shadow'
                }
            },
            legend: {
                data: types
            },
            xAxis: {
                name: x_name,
                type: 'category',
                data: categories, // X 軸數據 (LOGIN)
                axisLabel: {
                    interval: 0, // 每個標籤都顯示
                    rotate: 45, // 旋轉標籤以避免重疊
                }
            },
            yAxis: {
                name: y_name,
                type: 'value'
            },
            series: series // 使用組裝好的 series 數據
        };

        // 使用指定的配置項和數據顯示圖表
        chart.setOption(chartOption);
    });

    // 手動觸發一次 draw 事件以初始化圖表
    table.draw();
}

function generateBarChartWithThreshold(tableId, chartId, x_name, y_name, se_name, x_feild, y_filed, initialThreshold) {
    // 初始化 DataTable
    const table = $('#' + tableId).DataTable();

    // 監聽 DataTable 切換頁面的事件
    table.on('draw', function () {
        // 提取當前頁面資料
        const currentPageData = table.rows({ page: 'current' }).nodes().toArray();

        const categories = []; // 儲存 X 軸標籤
        const seriesData = []; // 儲存 Y 軸數據

        // 提取表格行及數據
        currentPageData.forEach(function (row) {
            const cells = $(row).find('td');
            categories.push(cells.eq(x_feild).text());  // X 軸數據
            seriesData.push(parseInt(cells.eq(y_filed).text()));  // Y 軸數據 (轉換為整數)
        });

        // 初始化 ECharts 圖表
        const chartElement = document.getElementById(chartId);
        if (!chartElement) {
            console.error(`Chart container with ID ${chartId} not found.`);
            return;
        }

        const chart = echarts.init(chartElement);

        // 設置圖表選項
        const chartOption = {

            tooltip: {
                trigger: 'axis',
                axisPointer: {
                    type: 'shadow'
                }
            },
            grid: {
                bottom: 100  // 根據標籤的長度調整
            },
            xAxis: {
                name: x_name,
                type: 'category',
                data: categories,
                axisLabel: {
                    interval: 0,  // 每個標籤都顯示
                    rotate: 45,   // 標籤旋轉避免重疊
                }
            },
            yAxis: {
                name: y_name,
                type: 'value'
            },
            series: [{
                name: se_name,
                type: 'bar',
                data: seriesData,  // Y 軸數據
                itemStyle: {
                    color: '#00aaff'  // 設定條形顏色
                },
                markLine: {
                    data: [{
                        name: '警戒線',
                        yAxis: initialThreshold
                    }],
                    lineStyle: {
                        color: 'red',  // 設定警戒線顏色
                        type: 'solid'
                    },
                    label: {
                        formatter: '警戒線: {c}'  // 標示警戒線
                    }
                }
            }],
            
        };

        // 使用指定的配置項和數據顯示圖表
        chart.setOption(chartOption);

        
    });

    // 手動觸發一次 draw 事件以初始化圖表
    table.draw();
}

function generateloadingChart(dataLoading, chartId) {
    var xAxisData = [];
    var cpuData = [];
    var memoryData = [];
    var networkData = [];

    // 遍歷數據，提取 X 軸和 Y 軸的資料
    dataLoading.forEach(function (row) {
        //xAxisData.push(row.INPUT_TIME);
        xAxisData.push(row.SERVER_TIME);
        cpuData.push(row.CPU);
        memoryData.push(row.FREEMEMORY / 1024); // 將 FREEMEMORY 換算成 MB
        networkData.push(row.NETWORK);
    });

    // 初始化圖表
    var chartDom = document.getElementById(chartId);
    var myChart = echarts.init(chartDom, 'dark');
    var option = {

        tooltip: {
            trigger: 'axis'
        },
        legend: {
            data: ['CPU', 'FreeMemory', 'Network']
        },
        xAxis: {
            type: 'category',
            data: xAxisData
        },
        yAxis: [
            {
                type: 'value',
                name: 'CPU (%)',
                position: 'left'
            },
            {
                type: 'value',
                name: 'Memory',
                position: 'right',
                axisLabel: {
                    formatter: '{value} MB'
                }
            },
            {
                type: 'value',
                name: 'Network',
                position: 'right',
                offset: 60
            }
        ],
        series: [
            {
                name: 'CPU',
                type: 'line',
                data: cpuData
            },
            {
                name: 'FreeMemory',
                type: 'line',
                yAxisIndex: 1,
                data: memoryData
            },
            {
                name: 'Network',
                type: 'line',
                yAxisIndex: 2,
                data: networkData
            }
        ],
        dataZoom: [
            {
                type: 'slider',
                start: 0,
                end: 100
            },
            {
                type: 'inside',
                start: 0,
                end: 100
            }
        ]
    };

    myChart.setOption(option);

    // 點擊事件
    myChart.getZr().on('click', function (params) {
        var pointInPixel = [params.offsetX, params.offsetY];
        if (myChart.containPixel('grid', pointInPixel)) {
            let xIndex = Math.round(myChart.convertFromPixel({ seriesIndex: 0 }, pointInPixel)[0]);
            let week = option.xAxis.data[xIndex];

            if (confirm(`您確定要跳轉到 ${week} 的資料頁面嗎？`)) {
                $.ajax({
                    url: '/Universal/Insert_Usage_Log',
                    type: 'POST',
                    data: {
                        Tool: '健康報告',
                        Remark: '執行更新(by圖)'
                    },
                    success: function () {
                        console.log("Log Insert Success.");
                    },
                    error: function (xhr, status, error) {
                        console.error("Error triggering log insertion:", error);
                    }
                });
                var selectedBrand_chart = document.getElementById('brandSelect').value;
                var selectedServer_chart = document.getElementById('serverSelect').value;
                //var selectedTime2 = document.getElementById('time2').value;
                var newUrl = `ServerHealth?brandselect=${encodeURIComponent(selectedBrand_chart)}&serverselect=${encodeURIComponent(selectedServer_chart)}&time=${encodeURIComponent(week)}&endTime=${encodeURIComponent(week)}`;
                //console.log(newUrl);
                window.location.href = newUrl;
            }
        }
    });
}


function initializeDataTable(tableId, defaultPageLength) {
    return $('#' + tableId).DataTable({
        responsive: true,
        scrollX: true,
        columnDefs: [
            { className: "text-center", targets: "_all" }
        ],
        "bInfo": false,
        pageLength: defaultPageLength || 10 // 默認顯示的筆數，傳遞參數或使用 10
        //lengthMenu: [[10, 20], [10, 20]] // 只顯示 10 和 20 筆資料的選項
    });
}


function addCustomFilter(tableId, filterIds, type_field, validStatuses) {
    $.fn.dataTable.ext.search.push(function (settings, data, dataIndex) {
        // 檢查是否是對應的表格
        if (settings.nTable.id !== tableId) {
            return true;  
        }
        // 取得每個篩選條件的值 (checked 或未勾選)
        var isFiltered = filterIds.some(function (filterId, index) {
            return $(filterId).prop('checked') && data[type_field] === validStatuses[index];
        });

        // 如果有一個條件符合則返回 true，否則檢查所有篩選是否都未選擇
        return isFiltered || filterIds.every(function (filterId) {
            return !$(filterId).prop('checked');
        });
    });
}

// 初始化篩選邏輯
function initTableFilters(filterIds, table) {
    $(filterIds.join(',')).on('change', function () {
        table.draw();
    });
}

function generateTimeFilterOptions(table, filterId, time_index) {
    if (table.rows().data().length === 0) {
        $(filterId).empty();
        $(filterId).append('<option value="">---No Data---</option>');
        return;
    }

    var times = new Set();

    table.rows().data().each(function (rowData) {
        var time = rowData[time_index];
        if (time) {
            times.add(time.trim());
        }
    });

    if (times.size === 0) {
        return;
    }

    function toISO(dateStr) {
        return dateStr
            .replace("上午", "AM")
            .replace("下午", "PM")
            .replace(/(\d{4})\/(\d{1,2})\/(\d{1,2}) (AM|PM) (\d{2}):(\d{2}):(\d{2})/, function (_, y, m, d, meridian, h, min, s) {
                h = parseInt(h, 10);
                if (meridian === "PM" && h !== 12) h += 12;
                if (meridian === "AM" && h === 12) h = 0;
                return `${y}-${m.padStart(2, '0')}-${d.padStart(2, '0')}T${h.toString().padStart(2, "0")}:${min}:${s}`;
            });
    }

    var sortedTimes = Array.from(times).sort(function (a, b) {
        var dateA = new Date(toISO(a));
        var dateB = new Date(toISO(b));
        return dateA - dateB;
    });

    //$(filterId).empty();

    sortedTimes.forEach(function (time) {
        $(filterId).append(`<option value="${time}">${time}</option>`);
    });

    $(filterId).val(sortedTimes[0]);
}


// 解析日期時間函數，將非標準格式轉換為標準 ISO 格式  
function parseDateTime(dateTime) {
    var match = dateTime.match(/(\d{4}\/\d{2}\/\d{2})\s+(上午|下午)?\s*(\d{1,2}):(\d{2}):(\d{2})/);
    if (!match) return dateTime; // 如果格式不匹配，返回原始值

    var datePart = match[1]; // 日期部分 (YYYY/MM/DD)
    var period = match[2];   // 上午或下午
    var hours = parseInt(match[3], 10);
    var minutes = match[4];
    var seconds = match[5];

    // 處理上午和下午
    if (period === "下午" && hours < 12) {
        hours += 12;
    } else if (period === "上午" && hours === 12) {
        hours = 0; // 上午 12 點應該是午夜 0 點
    }

    // 格式化為標準 ISO 格式 (YYYY-MM-DDTHH:mm:ss)
    return `${datePart.replace(/\//g, "-")}T${hours.toString().padStart(2, "0")}:${minutes}:${seconds}`;
}


function addTimeFilter(table, filterIds, time_index) {
    if (table.rows().data().length === 0) {
        //console.warn("DataTable沒有資料,test message");
        return; // 無資料時直接返回
    }

    $.fn.dataTable.ext.search.push(function (settings, data, dataIndex) {
        // 確保篩選應用於指定的 DataTable
        if (settings.nTable.id !== table.table().node().id) {
            return true;
        }
        //console.log(data);
        //console.log(data[time_index]); 

        var selectedTime = $(filterIds).val();
        var rowTime = data[time_index]; 

        //console.log(selectedTime);
        // 如果選擇 "All  " 或選定的時間，顯示該行
        return !selectedTime || rowTime === selectedTime;
    });
    $(filterIds).on('change', function () {
        //console.log(filterIds);
        //console.log(table);
        table.draw();
    });
}

function addExcludeFilter(table, excludeGroupId, excludeLoginId, groupIndex, loginIndex) {
    if (table.rows().data().length === 0) {
        console.warn("DataTable 沒有資料，無法應用篩選器");
        return; // 如果表格無資料，直接返回
    }

    // 添加 DataTable 自定義篩選邏輯
    $.fn.dataTable.ext.search.push(function (settings, data, dataIndex) {
        // 確保篩選應用於指定的 DataTable
        if (settings.nTable.id !== table.table().node().id) {
            return true;
        }

        // 獲取篩選器的值
        var excludeGroupPattern = $(excludeGroupId).val();
        var excludeLogins = $(excludeLoginId).val();

        // 初始化通過條件
        var groupPass = true;
        var loginPass = true;

        // 檢查是否存在 groupIndex
        if (groupIndex !== undefined && groupIndex >= 0 && excludeGroupPattern) {
            var rowGroup = data[groupIndex] || ''; // 防止 group 值為 undefined
            var regex = new RegExp(excludeGroupPattern);
            groupPass = !regex.test(rowGroup); // 如果匹配則排除
        }

        // 檢查是否存在 loginIndex
        if (loginIndex !== undefined && loginIndex >= 0 && excludeLogins) {
            var rowLogin = data[loginIndex] || ''; // 防止 login 值為 undefined
            var excludeLoginArray = excludeLogins.split(',').map(function (login) {
                return login.trim();
            });
            loginPass = !excludeLoginArray.includes(rowLogin); // 如果匹配則排除
        }

        // 僅當 group 和 login 都通過時顯示該行
        return groupPass && loginPass;
    });

    // 當輸入框內容改變時重新繪製表格
    $(excludeGroupId + ', ' + excludeLoginId).on('input', function () {
        table.draw();
    });
}


function updateCheckBoxes(configData) {


    var serverConfig = configData[0]; 

    $('#NYCheckbox').prop('checked', serverConfig.NY === 1);
    $('#LDCheckbox').prop('checked', serverConfig.LD === 1);
    $('#PelicanCheckbox').prop('checked', serverConfig.Pelican === 1);
    $('#brokereeCheckbox').prop('checked', serverConfig.Brokeree === 1);
    $('#MTSCheckbox').prop('checked', serverConfig.MTS === 1);
    $('#PAMMCheckbox').prop('checked', serverConfig.PAMM === 1);
    $('#MAMCheckbox').prop('checked', serverConfig.MAM === 1);
    $('#BASIC_ACCOUNTCheckbox').prop('checked', serverConfig.BASIC_ACCOUNT === 1);
    $('#USCCheckbox').prop('checked', serverConfig.USC_ACCOUNT === 1);
    $('#SGCheckbox').prop('checked', serverConfig.SPECIAL_GROUP === 1);
    $('#SCCheckbox').prop('checked', serverConfig.SPECIAL_CURRENCY === 1);
}


function generateSingleLineChart(dataLoading, chartId, selectedMetric) {
    var xAxisData = [];
    var metricData = [];

    // 遍歷數據，提取 X 軸和 Y 軸的資料
    dataLoading.forEach(function (row) {
        xAxisData.push(row.INPUT_TIME);

        // 根據傳入的 selectedMetric 來選擇對應的數據
        if (selectedMetric === 'CPU') {
            metricData.push(row.CPU);
        } else if (selectedMetric === 'Memory') {
            metricData.push(row.FREEMEMORY / 1024); // 將 FREEMEMORY 換算成 MB
        } else if (selectedMetric === 'Network') {
            metricData.push(row.NETWORK);
        }
    });

    // 初始化圖表
    var chartDom = document.getElementById(chartId);
    var myChart = echarts.init(chartDom, 'dark');
    var option = {

        tooltip: {
            trigger: 'axis'
        },
        legend: {
            data: [selectedMetric]
        },
        xAxis: {
            type: 'category',
            data: xAxisData
        },
        yAxis: [
            {
                type: 'value',
                name: selectedMetric, // 根據選擇的數據動態設置
                position: 'left',
                axisLabel: {
                    formatter: function (value) {
                        // 動態設置 y 軸的單位
                        if (selectedMetric === 'Memory') {
                            return value + ' MB';
                        } else if (selectedMetric === 'CPU') {
                            return value + ' %';
                        } else if (selectedMetric === 'Network') {
                            return value;
                        }
                        return value;
                    }
                }
            }
        ],
        series: [
            {
                name: selectedMetric,
                type: 'line',
                data: metricData
            }
        ],
        dataZoom: [
            {
                type: 'slider',
                start: 0,
                end: 100
            },
            {
                type: 'inside',
                start: 0,
                end: 100
            }
        ]
    };

    myChart.setOption(option);

    // 點擊事件
    myChart.getZr().on('click', function (params) {
        var pointInPixel = [params.offsetX, params.offsetY];
        if (myChart.containPixel('grid', pointInPixel)) {
            let xIndex = Math.round(myChart.convertFromPixel({ seriesIndex: 0 }, pointInPixel)[0]);
            let week = option.xAxis.data[xIndex];

            if (confirm(`您確定要跳轉到 ${week} 的資料頁面嗎？`)) {
                var selectedBrand_chart = document.getElementById('brandSelect').value;
                var selectedServer_chart = document.getElementById('serverSelect').value;
                var newUrl = `ServerHealth?brandselect=${encodeURIComponent(selectedBrand_chart)}&serverselect=${encodeURIComponent(selectedServer_chart)}&time=${encodeURIComponent(week)}`;
                window.location.href = newUrl;
            }
        }
    });
}



